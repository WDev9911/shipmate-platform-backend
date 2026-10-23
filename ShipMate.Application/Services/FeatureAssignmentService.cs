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
    private readonly IGitHubOAuthService _gitHubService;
    private readonly IGitHubConnectionRepository _gitHubConnectionRepository;
    private readonly IEncryptionService _encryptionService;

    public FeatureAssignmentService(
        IProductDefinitionRepository productDefinitionRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IGitHubOAuthService gitHubService,
        IGitHubConnectionRepository gitHubConnectionRepository,
        IEncryptionService encryptionService)
    {
        _productDefinitionRepository = productDefinitionRepository;
        _assignmentRepository = assignmentRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _gitHubService = gitHubService;
        _gitHubConnectionRepository = gitHubConnectionRepository;
        _encryptionService = encryptionService;
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
            catch (AppException ex) when (ex is GitHubBranchCreationFailedException or GitHubNotConnectedException)
            {
            }
        }

        return ToDto(assignment);
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

    // Uses the token of the developer who took the feature: they are the one who will push to the branch.
    private async Task CreateBranchAsync(Workspace workspace, FeatureAssignment assignment)
    {
        var token = await GitHubAccessTokens.GetValidOrThrowAsync(
            _gitHubConnectionRepository, _encryptionService, assignment.AssignedUserId);
        await _gitHubService.CreateBranchFromDefaultAsync(
            token, workspace.GitHubRepoOwner!, workspace.GitHubRepoName!, assignment.BranchName);

        assignment.BranchCreatedAt = DateTime.UtcNow;
        await _assignmentRepository.SaveChangesAsync();
    }

    private static bool HasLinkedRepo(Workspace workspace) =>
        !string.IsNullOrEmpty(workspace.GitHubRepoOwner) && !string.IsNullOrEmpty(workspace.GitHubRepoName);

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
