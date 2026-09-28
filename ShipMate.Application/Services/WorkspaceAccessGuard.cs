using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class WorkspaceAccessGuard : IWorkspaceAccessGuard
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;

    public WorkspaceAccessGuard(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMemberRepository workspaceMemberRepository)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
    }

    public async Task<Workspace> GetWorkspaceAsMemberAsync(Guid userId, Guid workspaceId)
    {
        var (workspace, _) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);
        return workspace;
    }

    public async Task<Workspace> GetWorkspaceAsManagerAsync(Guid userId, Guid workspaceId)
    {
        var (workspace, membership) = await GetWorkspaceWithMembershipOrThrow(userId, workspaceId);

        if (membership.Role != WorkspaceMemberRole.Manager)
        {
            throw new WorkspacePermissionDeniedException();
        }

        return workspace;
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
