using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id);

    /// <summary>Workspaces this user is an active member of (includes ones they own).</summary>
    Task<List<Workspace>> GetByMemberUserIdAsync(Guid userId);

    /// <summary>True if this user is the active Manager of any workspace that has a GitHub repo linked.</summary>
    Task<bool> HasManagedWorkspaceWithLinkedGitHubRepoAsync(Guid userId);
    Task AddAsync(Workspace workspace);
    void Update(Workspace workspace);
    Task<bool> SaveChangesAsync();
}
