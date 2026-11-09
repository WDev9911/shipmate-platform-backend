using AutoMapper;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Security;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class WorkspaceService : IWorkspaceService
{
    private const string RepoInviteSent = "sent";
    private const string RepoInviteAlreadyHasAccess = "alreadyHasAccess";
    private const string RepoInviteFailed = "failed";
    private const string RepoInviteNoRepo = "noRepo";

    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGitHubConnectionRepository _gitHubConnectionRepository;
    private readonly IGitHubOAuthService _gitHubOAuthService;
    private readonly IGitHubAppService _gitHubAppService;
    private readonly IEncryptionService _encryptionService;
    private readonly IEmailSender _emailSender;
    private readonly IWorkspaceNotifier _notifier;
    private readonly IAuditTrail _auditTrail;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IMapper _mapper;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IProductDefinitionRepository productDefinitionRepository,
        IUserRepository userRepository,
        IGitHubConnectionRepository gitHubConnectionRepository,
        IGitHubOAuthService gitHubOAuthService,
        IGitHubAppService gitHubAppService,
        IEncryptionService encryptionService,
        IEmailSender emailSender,
        IWorkspaceNotifier notifier,
        IAuditTrail auditTrail,
        IFeatureAssignmentRepository assignmentRepository,
        IMapper mapper)
    {
        _notifier = notifier;
        _auditTrail = auditTrail;
        _assignmentRepository = assignmentRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _productDefinitionRepository = productDefinitionRepository;
        _userRepository = userRepository;
        _gitHubConnectionRepository = gitHubConnectionRepository;
        _gitHubOAuthService = gitHubOAuthService;
        _gitHubAppService = gitHubAppService;
        _encryptionService = encryptionService;
        _emailSender = emailSender;
        _mapper = mapper;
    }

    public async Task<WorkspaceDto> CreateAsync(Guid ownerId, CreateWorkspaceRequest request)
    {
        var workspace = new Workspace
        {
            OwnerId = ownerId,
            Name = request.Name,
            VisionPrompt = request.VisionPrompt,
            LaunchDeadline = request.LaunchDeadline,
            Status = WorkspaceStatus.Active
        };
        await _workspaceRepository.AddAsync(workspace);

        // The creator is automatically the workspace's Manager (matches LOCK's solo-dev rule:
        // a lone active member is always Manager; here they start as the only member either way).
        await _workspaceMemberRepository.AddAsync(new WorkspaceMember
        {
            WorkspaceId = workspace.Id,
            UserId = ownerId,
            Role = WorkspaceMemberRole.Manager,
            Status = WorkspaceMemberStatus.Active
        });

        await _workspaceRepository.SaveChangesAsync();

        return _mapper.Map<WorkspaceDto>(workspace);
    }

    public async Task<List<WorkspaceDto>> GetMyWorkspacesAsync(Guid userId)
    {
        var workspaces = await _workspaceRepository.GetByMemberUserIdAsync(userId);
        return _mapper.Map<List<WorkspaceDto>>(workspaces);
    }

    public async Task<WorkspaceDto> GetByIdAsync(Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);
        return _mapper.Map<WorkspaceDto>(workspace);
    }

    public async Task<WorkspaceDto> UpdateAsync(Guid userId, Guid workspaceId, UpdateWorkspaceRequest request)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        if (request.Name is not null)
        {
            workspace.Name = request.Name;
        }

        var now = DateTime.UtcNow;

        if (request.VisionPrompt is not null && request.VisionPrompt != workspace.VisionPrompt)
        {
            // The vision prompt is DEFINE's input, so changing it also changes the product definition —
            // and once that is locked, the prompt is frozen with it.
            var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId);
            if (productDefinition is { IsLocked: true })
            {
                throw new ProductDefinitionLockedException();
            }

            workspace.VisionPrompt = request.VisionPrompt;
            productDefinition?.MarkModified(now);
        }

        if (request.LaunchDeadline is not null)
        {
            workspace.LaunchDeadline = request.LaunchDeadline;
        }

        workspace.UpdatedAt = now;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();

        return _mapper.Map<WorkspaceDto>(workspace);
    }

    public async Task ArchiveAsync(Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        workspace.Status = WorkspaceStatus.Archived;
        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();
    }

    public async Task<List<WorkspaceMemberDto>> GetMembersAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var members = await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspaceId);
        return _mapper.Map<List<WorkspaceMemberDto>>(members);
    }

    public async Task<WorkspaceMemberDto> AddMemberAsync(Guid userId, Guid workspaceId, AddMemberRequest request)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var targetUser = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new NotFoundException($"No user found with email '{request.Email}'.");

        var existing = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, targetUser.Id);
        if (existing is not null && existing.Status != WorkspaceMemberStatus.Removed)
        {
            throw new WorkspaceMemberAlreadyExistsException();
        }

        // Adding someone doesn't make them a member right away — they must accept first.
        WorkspaceMember member;
        if (existing is not null)
        {
            // Re-invite a previously removed/declined member instead of creating a duplicate row.
            existing.Status = WorkspaceMemberStatus.Invited;
            existing.Role = WorkspaceMemberRole.Developer;
            existing.UpdatedAt = DateTime.UtcNow;
            _workspaceMemberRepository.Update(existing);
            member = existing;
        }
        else
        {
            member = new WorkspaceMember
            {
                WorkspaceId = workspaceId,
                UserId = targetUser.Id,
                Role = WorkspaceMemberRole.Developer,
                Status = WorkspaceMemberStatus.Invited
            };
            await _workspaceMemberRepository.AddAsync(member);
        }

        await _workspaceRepository.SaveChangesAsync();

        await _emailSender.TrySendAsync(
            targetUser.Email,
            $"You've been invited to \"{workspace.Name}\" on ShipMate",
            $"""
             <p>Hi {targetUser.DisplayName},</p>
             <p>You've been invited to join the workspace <strong>{workspace.Name}</strong> on ShipMate.</p>
             <p>Log in and check your pending invitations to accept or decline.</p>
             """);

        member.User = targetUser;
        return _mapper.Map<WorkspaceMemberDto>(member);
    }

    public async Task<WorkspaceMemberDto> UpdateMemberRoleAsync(
        Guid userId, Guid workspaceId, Guid memberUserId, UpdateMemberRoleRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var targetMembership = await GetActiveMembershipOrThrow(workspaceId, memberUserId);

        // Exactly one Manager per workspace, always — this isn't "add a role", it's "transfer it".
        if (request.Role == WorkspaceMemberRole.Developer && targetMembership.Role == WorkspaceMemberRole.Manager)
        {
            throw new WorkspaceMustHaveManagerException();
        }

        if (request.Role == WorkspaceMemberRole.Manager && targetMembership.Role != WorkspaceMemberRole.Manager)
        {
            var members = await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspaceId);
            var currentManager = members.FirstOrDefault(m => m.Role == WorkspaceMemberRole.Manager);
            if (currentManager is not null)
            {
                currentManager.Role = WorkspaceMemberRole.Developer;
                currentManager.UpdatedAt = DateTime.UtcNow;
                _workspaceMemberRepository.Update(currentManager);
            }
        }

        targetMembership.Role = request.Role;
        targetMembership.UpdatedAt = DateTime.UtcNow;
        _workspaceMemberRepository.Update(targetMembership);

        await _workspaceRepository.SaveChangesAsync();

        return _mapper.Map<WorkspaceMemberDto>(targetMembership);
    }

    public async Task<WorkspaceMemberDto> UpdateMySkillAsync(Guid userId, Guid workspaceId, UpdateMySkillRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var membership = await GetActiveMembershipOrThrow(workspaceId, userId);
        membership.SkillLevel = request.SkillLevel;
        membership.UpdatedAt = DateTime.UtcNow;
        _workspaceMemberRepository.Update(membership);

        await _workspaceRepository.SaveChangesAsync();
        await NotifyBackupMatchesAsync(workspaceId, userId, request.SkillLevel);

        return _mapper.Map<WorkspaceMemberDto>(membership);
    }

    public async Task RemoveMemberAsync(Guid userId, Guid workspaceId, Guid memberUserId, bool force = false)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var targetMembership = await GetActiveMembershipOrThrow(workspaceId, memberUserId);

        // Can't leave a workspace with zero Managers — transfer the role to someone else first.
        if (targetMembership.Role == WorkspaceMemberRole.Manager)
        {
            throw new WorkspaceMustHaveManagerException();
        }

        // Membership is what grants access to the repository, so the access goes first: if GitHub can't take it away,
        // the member stays, and removing again later finishes the job. A forced removal skips this and leaves a record.
        var gitHubUsername = targetMembership.User?.GitHubUsername;
        try
        {
            await RevokeFromRepoAsync(workspace, gitHubUsername);
        }
        catch (GitHubRevokeFailedException) when (force)
        {
            await _auditTrail.AppendAsync(workspaceId, AuditTypes.RepoRevokeSkipped, userId, new { memberUserId, gitHubUsername });
        }

        targetMembership.Status = WorkspaceMemberStatus.Removed;
        targetMembership.UpdatedAt = DateTime.UtcNow;
        _workspaceMemberRepository.Update(targetMembership);

        await _workspaceRepository.SaveChangesAsync();
    }

    public async Task<List<WorkspaceInvitationDto>> GetMyInvitationsAsync(Guid userId)
    {
        var invitations = await _workspaceMemberRepository.GetPendingInvitationsByUserIdAsync(userId);
        return _mapper.Map<List<WorkspaceInvitationDto>>(invitations);
    }

    public async Task<WorkspaceMemberDto> AcceptInvitationAsync(Guid userId, Guid workspaceId, AcceptInvitationRequest request)
    {
        var invitation = await GetPendingInvitationOrThrow(userId, workspaceId);

        // Members get access to the repository, which goes by their GitHub name: nobody joins without one.
        var gitHubUsername = invitation.User?.GitHubUsername;
        if (string.IsNullOrWhiteSpace(gitHubUsername))
        {
            throw new GitHubRequiredToJoinException();
        }

        invitation.Status = WorkspaceMemberStatus.Active;
        invitation.SkillLevel = request.SkillLevel;
        invitation.UpdatedAt = DateTime.UtcNow;
        _workspaceMemberRepository.Update(invitation);

        await _workspaceRepository.SaveChangesAsync();
        await NotifyBackupMatchesAsync(workspaceId, userId, request.SkillLevel);
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        var repoInvite = await InviteToRepoAsync(workspace, gitHubUsername);

        var dto = _mapper.Map<WorkspaceMemberDto>(invitation);
        dto.RepoInvite = repoInvite;
        return dto;
    }

    // Joining or linking must not fail because GitHub does: a refused or failed invitation is left on record,
    // and the access is granted by hand.
    private async Task<string> InviteToRepoAsync(Workspace? workspace, string gitHubUsername)
    {
        if (workspace is not { GitHubInstallationId: { } installationId, GitHubRepoOwner: { } owner, GitHubRepoName: { } repo })
        {
            return RepoInviteNoRepo;
        }

        try
        {
            var sent = await _gitHubAppService.InviteCollaboratorAsync(installationId, owner, repo, gitHubUsername);
            return sent ? RepoInviteSent : RepoInviteAlreadyHasAccess;
        }
        catch (AppException ex) when (ex is GitHubRequestFailedException or GitHubAppUninstalledException)
        {
            // One record per save: each one is chained to the last record already stored.
            await _auditTrail.AppendAsync(workspace.Id, AuditTypes.RepoInviteFailed, null, new { gitHubUsername, reason = ex.ErrorCode });
            await _workspaceRepository.SaveChangesAsync();
            return RepoInviteFailed;
        }
    }

    // The repository was linked after these people joined: they get in now. The Manager linked it, so already has access.
    private async Task InviteMembersToRepoAsync(Workspace workspace)
    {
        var members = await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspace.Id);
        foreach (var member in members.Where(m =>
                     m.Status == WorkspaceMemberStatus.Active && m.Role != WorkspaceMemberRole.Manager))
        {
            if (!string.IsNullOrWhiteSpace(member.User?.GitHubUsername))
            {
                await InviteToRepoAsync(workspace, member.User.GitHubUsername);
            }
        }
    }

    // Takes a member's access to the repository away. Not finishing is an error, not something to swallow: a member who is
    // gone from the workspace but still holds access is exactly what this is meant to prevent.
    private async Task RevokeFromRepoAsync(Workspace workspace, string? gitHubUsername)
    {
        if (string.IsNullOrWhiteSpace(gitHubUsername)
            || workspace is not { GitHubInstallationId: { } installationId, GitHubRepoOwner: { } owner, GitHubRepoName: { } repo })
        {
            return;
        }

        try
        {
            await _gitHubAppService.RemoveCollaboratorAsync(installationId, owner, repo, gitHubUsername);
        }
        catch (GitHubRequestFailedException)
        {
            throw new GitHubRevokeFailedException();
        }
    }

    // The repository is being left: its feature-branch ruleset and the required ShipMate check on the default branch go,
    // or the owner is left with a check nobody reports and a main that can't be merged into. A failure doesn't hold the
    // unlink up (there is nothing to retry once the link is gone); it is recorded so the owner knows to clean up by hand.
    private async Task RemoveRepoLockAsync(Workspace workspace)
    {
        if (workspace is not { GitHubInstallationId: { } installationId, GitHubRepoOwner: { } owner, GitHubRepoName: { } repo })
        {
            return;
        }

        try
        {
            await _gitHubAppService.RemoveRepoLockAsync(installationId, owner, repo);
        }
        catch (AppException ex) when (ex is GitHubRequestFailedException or GitHubAppUninstalledException)
        {
            await _auditTrail.AppendAsync(workspace.Id, AuditTypes.RepoCleanupFailed, null, new { owner, repo, reason = ex.ErrorCode });
        }
    }

    // The repository is being unlinked or swapped: every member loses it. The Manager keeps theirs, as it was never granted here.
    private async Task RevokeMembersFromRepoAsync(Workspace workspace)
    {
        var members = await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspace.Id);
        foreach (var member in members.Where(m =>
                     m.Status == WorkspaceMemberStatus.Active && m.Role != WorkspaceMemberRole.Manager))
        {
            await RevokeFromRepoAsync(workspace, member.User?.GitHubUsername);
        }
    }

    // A backup feature is waiting for someone skilled enough: tell the person whose skill just reached it.
    // Nothing needs scanning in the background, since the open backup features are right there in the latest snapshot.
    private async Task NotifyBackupMatchesAsync(Guid workspaceId, Guid userId, int skillLevel)
    {
        var snapshot = await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId);
        var matches = snapshot?.Features
            .Where(f => f.Status == SnapshotFeatureStatus.Backup && FeatureAssignmentPolicy.IsQualified(skillLevel, f.Complexity))
            .Select(f => new { featureId = f.FeatureId, featureName = f.Name })
            .ToList();
        if (matches is not { Count: > 0 })
        {
            return;
        }

        await _notifier.PublishToUsersAsync([userId], WorkspaceEvents.BackupFeatureMatched, new { workspaceId, features = matches });
    }

    public async Task DeclineInvitationAsync(Guid userId, Guid workspaceId)
    {
        var invitation = await GetPendingInvitationOrThrow(userId, workspaceId);

        invitation.Status = WorkspaceMemberStatus.Removed;
        invitation.UpdatedAt = DateTime.UtcNow;
        _workspaceMemberRepository.Update(invitation);

        await _workspaceRepository.SaveChangesAsync();
    }

    private async Task<WorkspaceMember> GetPendingInvitationOrThrow(Guid userId, Guid workspaceId)
    {
        var invitation = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, userId);
        if (invitation is null || invitation.Status != WorkspaceMemberStatus.Invited)
        {
            throw new NotFoundException($"No pending invitation for workspace '{workspaceId}'.");
        }

        return invitation;
    }

    public async Task<List<GitHubRepoDto>> GetAvailableGitHubReposAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var accessToken = await GetValidGitHubAccessTokenOrThrow(userId);

        return await _gitHubOAuthService.GetUserRepositoriesAsync(accessToken);
    }

    public async Task<WorkspaceDto> LinkGitHubRepoAsync(Guid userId, Guid workspaceId, LinkGitHubRepoRequest request)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var accessToken = await GetValidGitHubAccessTokenOrThrow(userId);

        // Don't trust the owner/name strings blindly — confirm the Manager actually has access
        // to this exact repo before linking it (avoid linking a repo they don't own/can't reach).
        var accessibleRepos = await _gitHubOAuthService.GetUserRepositoriesAsync(accessToken);
        var isAccessible = accessibleRepos.Any(r =>
            string.Equals(r.Owner, request.Owner, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Name, request.Name, StringComparison.OrdinalIgnoreCase));

        if (!isAccessible)
        {
            throw new GitHubRepoNotAccessibleException();
        }

        // The Bot does all the writing on the repo, so the App must be installed there first.
        var installation = await _gitHubAppService.FindInstallationAsync(request.Owner, request.Name)
            ?? throw new GitHubAppNotInstalledException(_gitHubAppService.InstallUrl);

        // Nothing to protect yet: say so now rather than leave the merge gate off with no way to switch it on later.
        if (!await _gitHubAppService.RepoHasCommitsAsync(installation.Id, request.Owner, request.Name))
        {
            throw new GitHubRepoEmptyException();
        }

        // Code reaches the default branch only through a pull request the Bot has put its check on. This comes before anything
        // is taken off the old repository: if the new one refuses, the old one must still be locked.
        var mergeGateEnabled = await _gitHubAppService.EnsureMergeGateAsync(
            installation.Id, installation.IsOrganization, request.Owner, request.Name);

        // Moving to another repository: nobody keeps access to the old one. Nothing is changed if that can't be done.
        var movesToOtherRepo = workspace.GitHubInstallationId is not null
            && !(string.Equals(workspace.GitHubRepoOwner, request.Owner, StringComparison.OrdinalIgnoreCase)
                 && string.Equals(workspace.GitHubRepoName, request.Name, StringComparison.OrdinalIgnoreCase));
        if (movesToOtherRepo)
        {
            await RevokeMembersFromRepoAsync(workspace);
            await RemoveRepoLockAsync(workspace);
        }

        workspace.GitHubRepoOwner = request.Owner;
        workspace.GitHubRepoName = request.Name;
        workspace.GitHubInstallationId = installation.Id;
        workspace.GitHubRepoIsOrganization = installation.IsOrganization;
        workspace.GitHubMergeGateEnabled = mergeGateEnabled;

        // Webhook secret generated now, ready for a GitHub webhook to be registered and verified against.
        var (webhookSecretPlain, _) = SecureTokenGenerator.GenerateTokenPair();
        workspace.WebhookSecretEncrypted = _encryptionService.Encrypt(webhookSecretPlain);

        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();

        // Members who joined before the repository existed (or before it moved) get in now.
        await InviteMembersToRepoAsync(workspace);

        // Unlinking took the lock off the branches already handed out; they are locked again, in the new repository if it moved.
        await LockHeldBranchesAsync(workspace, movesToOtherRepo);

        return _mapper.Map<WorkspaceDto>(workspace);
    }

    // A branch that can't be locked now is marked as not created, so its holder retries it through the usual retry.
    private async Task LockHeldBranchesAsync(Workspace workspace, bool repoChanged)
    {
        var held = (await _assignmentRepository.GetByWorkspaceIdAsync(workspace.Id))
            .Where(a => a.ReleasedAt is null && a.BranchCreatedAt is not null)
            .ToList();
        if (held.Count == 0)
        {
            return;
        }

        foreach (var assignment in held)
        {
            try
            {
                await _gitHubAppService.CreateProtectedBranchAsync(
                    workspace.GitHubInstallationId!.Value, workspace.GitHubRepoIsOrganization,
                    workspace.GitHubRepoOwner!, workspace.GitHubRepoName!, assignment.BranchName);
            }
            catch (AppException)
            {
                assignment.BranchCreatedAt = null;
            }

            // Commits approved in the old repository say nothing about the branch in a new one.
            if (repoChanged)
            {
                assignment.LastApprovedSha = null;
                assignment.FlaggedHeadSha = null;
            }
        }

        await _assignmentRepository.SaveChangesAsync();
    }

    public async Task UnlinkGitHubRepoAsync(Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        // Members lose the repository, and ShipMate's rules come off it, before the link is forgotten: the link is what
        // says where the repository is.
        await RevokeMembersFromRepoAsync(workspace);
        await RemoveRepoLockAsync(workspace);

        workspace.GitHubRepoOwner = null;
        workspace.GitHubRepoName = null;
        workspace.GitHubInstallationId = null;
        workspace.GitHubRepoIsOrganization = false;
        workspace.GitHubMergeGateEnabled = false;
        workspace.LastAuditedMainSha = null;
        workspace.WebhookSecretEncrypted = null;
        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();
    }

    private Task<string> GetValidGitHubAccessTokenOrThrow(Guid userId) =>
        GitHubAccessTokens.GetValidOrThrowAsync(_gitHubConnectionRepository, _gitHubOAuthService, _encryptionService,
            _workspaceRepository.SaveChangesAsync, userId);

    private async Task<WorkspaceMember> GetActiveMembershipOrThrow(Guid workspaceId, Guid memberUserId)
    {
        var membership = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, memberUserId);
        if (membership is null || membership.Status != WorkspaceMemberStatus.Active)
        {
            throw new NotFoundException("WorkspaceMember", memberUserId);
        }

        return membership;
    }
}
