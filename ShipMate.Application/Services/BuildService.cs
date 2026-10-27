using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class BuildService : IBuildService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IBuildSubmissionRepository _submissionRepository;
    private readonly IGitHubAppService _gitHubAppService;
    private readonly IAuditTrail _auditTrail;
    private readonly IDirectPushDetector _directPushDetector;
    private readonly IBuildAnalysisQueue _analysisQueue;

    public BuildService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IWorkspaceRepository workspaceRepository,
        IProductDefinitionRepository productDefinitionRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IBuildSubmissionRepository submissionRepository,
        IGitHubAppService gitHubAppService,
        IAuditTrail auditTrail,
        IDirectPushDetector directPushDetector,
        IBuildAnalysisQueue analysisQueue)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _workspaceRepository = workspaceRepository;
        _productDefinitionRepository = productDefinitionRepository;
        _assignmentRepository = assignmentRepository;
        _submissionRepository = submissionRepository;
        _gitHubAppService = gitHubAppService;
        _auditTrail = auditTrail;
        _directPushDetector = directPushDetector;
        _analysisQueue = analysisQueue;
    }

    public async Task<BuildSubmissionDto> GetSubmissionAsync(Guid userId, Guid workspaceId, Guid submissionId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var submission = await _submissionRepository.GetByIdAsync(submissionId);
        if (submission is null || submission.WorkspaceId != workspaceId)
        {
            throw new NotFoundException("Build submission", submissionId);
        }

        return BuildSubmissionDto.From(submission);
    }

    public async Task<BuildSubmissionDto> SubmitAsync(Guid userId, Guid workspaceId, SubmitBuildRequest request)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        if (string.IsNullOrEmpty(workspace.GitHubRepoOwner) || string.IsNullOrEmpty(workspace.GitHubRepoName)
            || workspace.GitHubInstallationId is null)
        {
            throw new WorkspaceRepoNotLinkedException();
        }

        await EnsureGovernanceIsActiveAsync(workspace);

        switch (BuildBranchPolicy.Classify(request.BranchName, out var featureId))
        {
            case BuildBranchKind.Protected:
                throw new ProtectedBranchSubmitException();
            case BuildBranchKind.Hotfix:
                throw new HotfixNotSupportedException();
            case BuildBranchKind.Invalid:
                throw new InvalidFeatureIdException();
        }

        var snapshot = await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId)
            ?? throw new NotFoundException("MVP snapshot", workspaceId);

        var feature = snapshot.Features.FirstOrDefault(f => f.FeatureId == featureId)
            ?? throw new InvalidFeatureIdException();

        if (feature.Status == SnapshotFeatureStatus.Replaced)
        {
            throw new FeatureReplacedException();
        }

        if (feature.Status == SnapshotFeatureStatus.Backup)
        {
            throw new FeatureIsBackupException();
        }

        var assignment = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .FirstOrDefault(a => a.FeatureId == featureId && a.ReleasedAt is null)
            ?? throw new FeatureNotAssignedException();

        if (assignment.AssignedUserId != userId)
        {
            throw new FeatureAssignedToOtherException();
        }

        // The feature is yours, but the branch name must be the one ShipMate registered for you.
        if (assignment.BranchName != request.BranchName)
        {
            throw new InvalidFeatureIdException();
        }

        var stagingBranch = BuildBranchPolicy.StagingBranchFor(request.BranchName);
        var stagingHead = await GetStagingHeadAsync(workspace, stagingBranch);
        if (!string.Equals(stagingHead, request.CommitSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new CommitNotInStagingException();
        }

        // Safety net for repos GitHub can't lock: code that bypassed ShipMate is written to the audit trail, saved with this submit.
        await _directPushDetector.DetectAsync(workspace, assignment);

        var submission = new BuildSubmission
        {
            WorkspaceId = workspaceId,
            FeatureId = featureId,
            SubmittedByUserId = userId,
            StagingBranch = stagingBranch,
            HeadSha = stagingHead!,
            SnapshotVersion = snapshot.Version
        };
        // Only the newest commit of a feature is analysed: older submits still waiting are replaced.
        foreach (var older in await _submissionRepository.GetAnalyzingAsync(workspaceId, featureId))
        {
            older.Status = BuildSubmissionStatus.Superseded;
        }

        await _submissionRepository.AddAsync(submission);
        await _submissionRepository.SaveChangesAsync();
        _analysisQueue.Enqueue(submission.Id, featureId);

        return BuildSubmissionDto.From(submission);
    }

    // A suspended workspace is checked again on every submit: once the App is installed again the flag clears itself.
    private async Task EnsureGovernanceIsActiveAsync(Workspace workspace)
    {
        if (workspace.GovernanceSuspendedAt is null)
        {
            return;
        }

        var installation = await _gitHubAppService.FindInstallationAsync(
            workspace.GitHubRepoOwner!, workspace.GitHubRepoName!);
        if (installation is null)
        {
            throw new GovernanceSuspendedException();
        }

        workspace.GovernanceSuspendedAt = null;
        workspace.GovernanceSuspendedReason = null;
        workspace.GitHubInstallationId = installation.Id;
        workspace.GitHubRepoIsOrganization = installation.IsOrganization;
        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);
        await _workspaceRepository.SaveChangesAsync();
    }

    // Reading the staging branch is where an uninstalled App shows up: GitHub no longer hands out a token for it.
    private async Task<string?> GetStagingHeadAsync(Workspace workspace, string stagingBranch)
    {
        try
        {
            return await _gitHubAppService.GetBranchHeadShaAsync(
                workspace.GitHubInstallationId!.Value, workspace.GitHubRepoOwner!, workspace.GitHubRepoName!, stagingBranch);
        }
        catch (GitHubAppUninstalledException)
        {
            await _auditTrail.AppendAsync(workspace.Id, AuditTypes.SystemAlert, null, new
            {
                reason = Workspace.AppUninstalledReason,
                repository = $"{workspace.GitHubRepoOwner}/{workspace.GitHubRepoName}"
            });

            workspace.GovernanceSuspendedAt = DateTime.UtcNow;
            workspace.GovernanceSuspendedReason = Workspace.AppUninstalledReason;
            workspace.UpdatedAt = DateTime.UtcNow;
            _workspaceRepository.Update(workspace);
            await _workspaceRepository.SaveChangesAsync();

            throw new GovernanceSuspendedException();
        }
    }
}
