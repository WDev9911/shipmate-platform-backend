using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Services;

public interface IWorkspaceAccessGuard
{
    /// <summary>Returns the workspace if the user is an active member of it; otherwise throws NotFoundException.</summary>
    Task<Workspace> GetWorkspaceAsMemberAsync(Guid userId, Guid workspaceId);

    /// <summary>Like GetWorkspaceAsMemberAsync, but also throws WorkspacePermissionDeniedException unless the user is its Manager.</summary>
    Task<Workspace> GetWorkspaceAsManagerAsync(Guid userId, Guid workspaceId);
}
