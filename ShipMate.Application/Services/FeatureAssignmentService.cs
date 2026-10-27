using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class FeatureAssignmentService : IFeatureAssignmentService
{
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IGitHubAppService _gitHubAppService;
    private readonly IWorkspaceNotifier _notifier;

    public FeatureAssignmentService(
        IProductDefinitionRepository productDefinitionRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IGitHubAppService gitHubAppService,
        IWorkspaceNotifier notifier)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _assignmentRepository = assignmentRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _gitHubAppService = gitHubAppService;
        _notifier = notifier;
    }

    public async Task<List<FeatureAssignmentOverviewDto>> GetOverviewAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var snapshot = await GetSnapshotOrThrow(workspaceId);
        var members = await GetActiveMembers(workspaceId);
        var assignments = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(a => a.ReleasedAt is null)
            .ToDictionary(a => a.FeatureId);

        return snapshot.Features
            .Where(f => f.Status != SnapshotFeatureStatus.Replaced)
            .Select(feature => new FeatureAssignmentOverviewDto
            {
                FeatureId = feature.FeatureId,
                Name = feature.Name,
                Status = feature.Status,
                Complexity = feature.Complexity,
                Assignee = assignments.TryGetValue(feature.FeatureId, out var assignment)
                    ? new FeatureAssigneeDto(assignment.AssignedUserId, assignment.AssignedUser.DisplayName,
                        assignment.BranchName, assignment.BranchCreatedAt is not null)
                    : null,
                QualifiedMembers = members
                    .Where(m => FeatureAssignmentPolicy.IsQualified(m.SkillLevel, feature.Complexity))
                    .Select(m => new QualifiedMemberDto(m.UserId, m.User.DisplayName, m.SkillLevel!.Value))
                    .ToList()
            })
            .ToList();
    }

    public async Task<FeatureAssignmentDto> ClaimAsync(Guid userId, Guid workspaceId, Guid featureId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var snapshot = await GetSnapshotOrThrow(workspaceId);
        var feature = snapshot.Features.FirstOrDefault(f => f.FeatureId == featureId && f.Status != SnapshotFeatureStatus.Replaced)
            ?? throw new NotFoundException("Feature", featureId);

        var member = (await GetActiveMembers(workspaceId)).FirstOrDefault(m => m.UserId == userId)
            ?? throw new NotFoundException("Workspace", workspaceId);

        if (member.SkillLevel is null)
        {
            throw new SkillNotDeclaredException();
        }

        if (!FeatureAssignmentPolicy.IsQualified(member.SkillLevel, feature.Complexity))
        {
            throw new SkillTooLowForFeatureException();
        }

        if (string.IsNullOrWhiteSpace(member.User.GitHubUsername))
        {
            throw new GitHubNotConnectedException();
        }

        var assignment = new FeatureAssignment
        {
            WorkspaceId = workspaceId,
            FeatureId = featureId,
            AssignedUserId = userId,
            BranchName = FeatureAssignmentPolicy.BranchNameFor(featureId, member.User.GitHubUsername)
        };

        // A backup feature becomes locked in the same version once someone takes it, and the hash follows.
        var wasBackup = feature.Status == SnapshotFeatureStatus.Backup;
        if (wasBackup)
        {
            feature.Status = SnapshotFeatureStatus.Locked;
            snapshot.Hash = MvpSnapshotHasher.Compute(snapshot);
        }

        if (!await _assignmentRepository.TryAddAsync(assignment))
        {
            if (wasBackup)
            {
                feature.Status = SnapshotFeatureStatus.Backup;
                snapshot.Hash = MvpSnapshotHasher.Compute(snapshot);
            }

            throw new FeatureAlreadyAssignedException();
        }

        // The claim is already won; a branch GitHub refuses now can be retried, so it never undoes the claim.
        if (HasLinkedRepo(workspace))
        {
            try
            {
                await CreateBranchAsync(workspace, assignment);
            }
            catch (AppException ex) when (ex is GitHubBranchCreationFailedException or GitHubAppUninstalledException)
            {
            }
        }

        if (wasBackup)
        {
            await NotifyBackupClaimedAsync(workspaceId, snapshot, feature, userId);
        }

        return ToDto(assignment);
    }

    // The backup feature is taken, so those whose own feature waited on it can move on.
    private async Task NotifyBackupClaimedAsync(Guid workspaceId, MvpSnapshot snapshot, SnapshotFeature feature, Guid claimedBy)
    {
        var waiting = MvpSnapshotEvolution.DependentsOf(snapshot, feature.FeatureId).ToHashSet();
        var holders = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(a => a.ReleasedAt is null && waiting.Contains(a.FeatureId) && a.AssignedUserId != claimedBy)
            .Select(a => a.AssignedUserId);

        await _notifier.PublishToUsersAsync(holders, WorkspaceEvents.BackupFeatureClaimed, new
        {
            workspaceId,
            featureId = feature.FeatureId,
            featureName = feature.Name,
            claimedBy
        });
    }

    public async Task<FeatureAssignmentDto> RetryBranchAsync(Guid userId, Guid workspaceId, Guid featureId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var assignment = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .FirstOrDefault(a => a.FeatureId == featureId && a.AssignedUserId == userId && a.ReleasedAt is null)
            ?? throw new NotFoundException("Feature assignment", featureId);

        if (assignment.BranchCreatedAt is null)
        {
            if (!HasLinkedRepo(workspace))
            {
                throw new WorkspaceRepoNotLinkedException();
            }

            await CreateBranchAsync(workspace, assignment);
        }

        return ToDto(assignment);
    }

    // The Bot creates the branch and locks it right away, so the developer can only reach it through ShipMate.
    // BranchCreatedAt is set only once both are done, so a retry repeats the whole step.
    private async Task CreateBranchAsync(Workspace workspace, FeatureAssignment assignment)
    {
        await _gitHubAppService.CreateProtectedBranchAsync(
            workspace.GitHubInstallationId!.Value, workspace.GitHubRepoIsOrganization,
            workspace.GitHubRepoOwner!, workspace.GitHubRepoName!, assignment.BranchName);

        assignment.BranchCreatedAt = DateTime.UtcNow;
        await _assignmentRepository.SaveChangesAsync();
    }

    // A repo linked before the App existed has no installation; linking it again fills it in.
    private static bool HasLinkedRepo(Workspace workspace) =>
        !string.IsNullOrEmpty(workspace.GitHubRepoOwner) && !string.IsNullOrEmpty(workspace.GitHubRepoName)
        && workspace.GitHubInstallationId is not null;

    private static FeatureAssignmentDto ToDto(FeatureAssignment assignment) => new()
    {
        FeatureId = assignment.FeatureId,
        AssignedUserId = assignment.AssignedUserId,
        BranchName = assignment.BranchName,
        BranchCreated = assignment.BranchCreatedAt is not null
    };

    private async Task<MvpSnapshot> GetSnapshotOrThrow(Guid workspaceId) =>
        await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId)
            ?? throw new NotFoundException("MVP snapshot", workspaceId);

    private async Task<List<WorkspaceMember>> GetActiveMembers(Guid workspaceId) =>
        (await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(m => m.Status == WorkspaceMemberStatus.Active)
            .ToList();
}
