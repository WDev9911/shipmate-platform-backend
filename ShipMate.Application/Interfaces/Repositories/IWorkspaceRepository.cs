using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id);

    /// <summary>Read fresh and not tracked: the workspace as the database holds it now, even if this request already loaded it.</summary>
    Task<Workspace?> GetCurrentAsync(Guid id);

    /// <summary>Workspaces this user is an active member of (includes ones they own).</summary>
    Task<List<Workspace>> GetByMemberUserIdAsync(Guid userId);

    /// <summary>True if this user is an active member of any active workspace.</summary>
    Task<bool> IsActiveMemberOfAnyWorkspaceAsync(Guid userId);
    Task AddAsync(Workspace workspace);
    void Update(Workspace workspace);
    Task<bool> SaveChangesAsync();
}
