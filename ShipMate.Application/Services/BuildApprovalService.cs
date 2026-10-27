using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class BuildApprovalService : IBuildApprovalService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IBuildSubmissionRepository _submissionRepository;
    private readonly IDriftAlertRepository _alertRepository;
    private readonly IGitHubAppService _gitHubAppService;
    private readonly IWorkspaceNotifier _notifier;

    public BuildApprovalService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IProductDefinitionRepository productDefinitionRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IBuildSubmissionRepository submissionRepository,
        IDriftAlertRepository alertRepository,
        IGitHubAppService gitHubAppService,
        IWorkspaceNotifier notifier)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _productDefinitionRepository = productDefinitionRepository;
        _assignmentRepository = assignmentRepository;
        _submissionRepository = submissionRepository;
        _alertRepository = alertRepository;
        _gitHubAppService = gitHubAppService;
        _notifier = notifier;
    }

    public async Task<BuildSubmissionDto> ApproveAsync(
        Guid userId, Guid workspaceId, Guid submissionId, ApproveBuildRequest request)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        if (string.IsNullOrEmpty(workspace.GitHubRepoOwner) || string.IsNullOrEmpty(workspace.GitHubRepoName)
            || workspace.GitHubInstallationId is null)
        {
            throw new WorkspaceRepoNotLinkedException();
        }

        // The Bot can't act on a repo it lost, so there is nothing to move the branch with.
        if (workspace.GovernanceSuspendedAt is not null)
        {
            throw new GovernanceSuspendedException();
        }

        var submission = await _submissionRepository.GetByIdAsync(submissionId);
        if (submission is null || submission.WorkspaceId != workspaceId)
        {
            throw new NotFoundException("Build submission", submissionId);
        }

        var assignment = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .FirstOrDefault(a => a.FeatureId == submission.FeatureId && a.ReleasedAt is null);
        if (assignment is null || assignment.AssignedUserId != userId)
        {
            throw new NotFeatureHolderException();
        }

        var feature = await GetActiveFeatureAsync(workspaceId, submission.FeatureId);
        await EnsureReadyAsync(submission);

        var sha = request.CommitSha ?? submission.HeadSha;
        var installationId = workspace.GitHubInstallationId.Value;
        var owner = workspace.GitHubRepoOwner;
        var repo = workspace.GitHubRepoName;

        if (!string.Equals(sha, submission.HeadSha, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureCommitIsPendingAsync(installationId, owner, repo, assignment.BranchName, submission.HeadSha, sha);
        }

        // Before the branch moves: a failure here changes nothing and the whole approval can be repeated.
        await _gitHubAppService.CreateScopeCheckRunAsync(
            installationId, owner, repo, sha, "Scope check passed", DescribeCheck(submission, feature));

        if (!await _gitHubAppService.FastForwardBranchAsync(installationId, owner, repo, assignment.BranchName, sha))
        {
            throw new FastForwardFailedException();
        }

        var now = DateTime.UtcNow;
        submission.Status = BuildSubmissionStatus.Approved;
        submission.ApprovedSha = sha;
        submission.ApprovedByUserId = userId;
        submission.ApprovedAt = now;
        assignment.LastApprovedSha = sha;
        await _submissionRepository.SaveChangesAsync();

        await TryClearStagingAsync(installationId, owner, repo, submission.StagingBranch, sha);

        await _notifier.PublishAsync(workspaceId, WorkspaceEvents.CodeScopeApproved, new
        {
            submissionId = submission.Id,
            featureId = submission.FeatureId,
            featureName = feature.Name,
            approvedSha = sha,
            approvedBy = userId
        });
        await _notifier.PublishAsync(workspaceId, WorkspaceEvents.BranchPushSuccess, new
        {
            featureId = submission.FeatureId,
            branch = assignment.BranchName,
            sha
        });

        return BuildSubmissionDto.From(submission);
    }

    private async Task<SnapshotFeature> GetActiveFeatureAsync(Guid workspaceId, Guid featureId)
    {
        var snapshot = await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId)
            ?? throw new NotFoundException("MVP snapshot", workspaceId);
        var feature = snapshot.Features.FirstOrDefault(f => f.FeatureId == featureId)
            ?? throw new InvalidFeatureIdException();

        switch (feature.Status)
        {
            case SnapshotFeatureStatus.Replaced:
                throw new FeatureReplacedException();
            case SnapshotFeatureStatus.Backup:
                throw new FeatureIsBackupException();
        }

        return feature;
    }

    // Only the newest submission of a feature can be approved, and only with a verdict that lets the code through.
    private async Task EnsureReadyAsync(BuildSubmission submission)
    {
        var newest = await _submissionRepository.GetLatestAsync(submission.WorkspaceId, submission.FeatureId);
        var openAlerts = await _alertRepository.GetOpenByFeatureAsync(submission.WorkspaceId, submission.FeatureId);
        var latestAlert = await _alertRepository.GetLatestByFeatureAsync(submission.WorkspaceId, submission.FeatureId);

        switch (BuildApprovalPolicy.Evaluate(submission, newest, openAlerts.Count > 0, latestAlert?.Status))
        {
            case ApprovalReadiness.Superseded:
                throw new SubmissionSupersededException();
            case ApprovalReadiness.NotReady:
                throw new SubmissionNotReadyException();
            case ApprovalReadiness.DriftUnresolved:
                throw new DriftUnresolvedException();
        }
    }

    // Picking an earlier commit is only allowed among the ones the feature branch doesn't have yet.
    private async Task EnsureCommitIsPendingAsync(
        long installationId, string owner, string repo, string featureBranch, string headSha, string sha)
    {
        var baseRef = await _gitHubAppService.GetBranchHeadShaAsync(installationId, owner, repo, featureBranch)
            ?? await _gitHubAppService.GetDefaultBranchAsync(installationId, owner, repo);
        var pending = await _gitHubAppService.CompareAsync(installationId, owner, repo, baseRef, headSha);

        var isPending = pending?.CommitShas?.Any(c => string.Equals(c, sha, StringComparison.OrdinalIgnoreCase)) == true;
        if (!isPending)
        {
            throw new CommitNotInSubmissionException();
        }
    }

    private static string DescribeCheck(BuildSubmission submission, SnapshotFeature feature)
    {
        var verdict = submission.ConfidenceScore < submission.ThresholdAtCheck
            ? "The code went beyond the locked scope; a Manager overrode the alert."
            : "The code stays inside the locked scope.";

        return $"Feature: {feature.Name}\n"
            + $"Scope version: {submission.SnapshotVersion}\n"
            + $"Confidence: {submission.ConfidenceScore:0.00} (threshold {submission.ThresholdAtCheck:0.00})\n"
            + verdict;
    }

    // Tidying up after the approval already happened must not undo it: a staging branch that stays is harmless.
    private async Task TryClearStagingAsync(long installationId, string owner, string repo, string stagingBranch, string sha)
    {
        try
        {
            var stagingHead = await _gitHubAppService.GetBranchHeadShaAsync(installationId, owner, repo, stagingBranch);
            if (string.Equals(stagingHead, sha, StringComparison.OrdinalIgnoreCase))
            {
                await _gitHubAppService.DeleteBranchAsync(installationId, owner, repo, stagingBranch);
            }
        }
        catch (AppException)
        {
        }
    }
}
