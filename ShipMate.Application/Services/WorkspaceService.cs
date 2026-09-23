using AutoMapper;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Security;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class WorkspaceService : IWorkspaceService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IUserRepository _userRepository;
    private readonly IGitHubConnectionRepository _gitHubConnectionRepository;
    private readonly IGitHubOAuthService _gitHubOAuthService;
    private readonly IEncryptionService _encryptionService;
    private readonly IEmailSender _emailSender;
    private readonly IMapper _mapper;

    public WorkspaceService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IUserRepository userRepository,
        IGitHubConnectionRepository gitHubConnectionRepository,
        IGitHubOAuthService gitHubOAuthService,
        IEncryptionService encryptionService,
        IEmailSender emailSender,
        IMapper mapper)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _userRepository = userRepository;
        _gitHubConnectionRepository = gitHubConnectionRepository;
        _gitHubOAuthService = gitHubOAuthService;
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
        var (workspace, _) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        return _mapper.Map<WorkspaceDto>(workspace);
    }

    public async Task<WorkspaceDto> UpdateAsync(Guid userId, Guid workspaceId, UpdateWorkspaceRequest request)
    {
        var (workspace, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

        if (request.Name is not null)
        {
            workspace.Name = request.Name;
        }

        if (request.VisionPrompt is not null)
        {
            workspace.VisionPrompt = request.VisionPrompt;
        }

        if (request.LaunchDeadline is not null)
        {
            workspace.LaunchDeadline = request.LaunchDeadline;
        }

        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();

        return _mapper.Map<WorkspaceDto>(workspace);
    }

    public async Task ArchiveAsync(Guid userId, Guid workspaceId)
    {
        var (workspace, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

        workspace.Status = WorkspaceStatus.Archived;
        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();
    }

    public async Task<List<WorkspaceMemberDto>> GetMembersAsync(Guid userId, Guid workspaceId)
    {
        await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);

        var members = await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspaceId);
        return _mapper.Map<List<WorkspaceMemberDto>>(members);
    }

    public async Task<WorkspaceMemberDto> AddMemberAsync(Guid userId, Guid workspaceId, AddMemberRequest request)
    {
        var (workspace, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

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

        await _emailSender.SendAsync(
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
        var (_, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

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

    public async Task RemoveMemberAsync(Guid userId, Guid workspaceId, Guid memberUserId)
    {
        var (_, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

        var targetMembership = await GetActiveMembershipOrThrow(workspaceId, memberUserId);

        // Can't leave a workspace with zero Managers — transfer the role to someone else first.
        if (targetMembership.Role == WorkspaceMemberRole.Manager)
        {
            throw new WorkspaceMustHaveManagerException();
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

    public async Task<WorkspaceMemberDto> AcceptInvitationAsync(Guid userId, Guid workspaceId)
    {
        var invitation = await GetPendingInvitationOrThrow(userId, workspaceId);

        invitation.Status = WorkspaceMemberStatus.Active;
        invitation.UpdatedAt = DateTime.UtcNow;
        _workspaceMemberRepository.Update(invitation);

        await _workspaceRepository.SaveChangesAsync();

        return _mapper.Map<WorkspaceMemberDto>(invitation);
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
        var (_, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

        var accessToken = await GetValidGitHubAccessTokenOrThrow(userId);

        return await _gitHubOAuthService.GetUserRepositoriesAsync(accessToken);
    }

    public async Task<WorkspaceDto> LinkGitHubRepoAsync(Guid userId, Guid workspaceId, LinkGitHubRepoRequest request)
    {
        var (workspace, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

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

        workspace.GitHubRepoOwner = request.Owner;
        workspace.GitHubRepoName = request.Name;

        // Webhook secret generated now, ready for BUILD to actually register the GitHub webhook
        // and verify incoming signatures against — BUILD itself isn't built yet.
        var (webhookSecretPlain, _) = SecureTokenGenerator.GenerateTokenPair();
        workspace.WebhookSecretEncrypted = _encryptionService.Encrypt(webhookSecretPlain);

        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();

        return _mapper.Map<WorkspaceDto>(workspace);
    }

    public async Task UnlinkGitHubRepoAsync(Guid userId, Guid workspaceId)
    {
        var (workspace, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        EnsureManager(membership);

        workspace.GitHubRepoOwner = null;
        workspace.GitHubRepoName = null;
        workspace.WebhookSecretEncrypted = null;
        workspace.UpdatedAt = DateTime.UtcNow;
        _workspaceRepository.Update(workspace);

        await _workspaceRepository.SaveChangesAsync();
    }

    private async Task<string> GetValidGitHubAccessTokenOrThrow(Guid userId)
    {
        var connection = await _gitHubConnectionRepository.GetByUserIdAsync(userId)
            ?? throw new GitHubNotConnectedException();

        if (connection.AccessTokenExpiresAt is not null && connection.AccessTokenExpiresAt < DateTime.UtcNow)
        {
            // Auto-refreshing via the stored GitHub refresh token is BUILD-phase work — for now,
            // just ask the Manager to sign in with GitHub again to get a fresh access token.
            throw new GitHubNotConnectedException();
        }

        return _encryptionService.Decrypt(connection.GitHubAccessTokenEncrypted);
    }

    private static void EnsureManager(WorkspaceMember membership)
    {
        if (membership.Role != WorkspaceMemberRole.Manager)
        {
            throw new WorkspacePermissionDeniedException();
        }
    }

    private async Task<WorkspaceMember> GetActiveMembershipOrThrow(Guid workspaceId, Guid memberUserId)
    {
        var membership = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, memberUserId);
        if (membership is null || membership.Status != WorkspaceMemberStatus.Active)
        {
            throw new NotFoundException("WorkspaceMember", memberUserId);
        }

        return membership;
    }

    // Access to a workspace (view or manage) requires active membership — not just being the
    // original owner. "Not found" covers both "doesn't exist" and "you're not a member",
    // so a user can't probe for workspaces they're not part of.
    private async Task<(Workspace Workspace, WorkspaceMember Membership)> GetWorkspaceWithMembershipOrThrow(
        Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId);
        var membership = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, userId);

        if (workspace is null || membership is null || membership.Status != WorkspaceMemberStatus.Active)
        {
            throw new NotFoundException("Workspace", workspaceId);
        }

        return (workspace, membership);
    }
}
