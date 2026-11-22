using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IShipCardRepository
{
    Task AddAsync(ShipCard card);
    Task<ShipCard?> GetByIdAsync(Guid workspaceId, Guid cardId);
    Task<ShipCard?> GetByVersionAsync(Guid workspaceId, int snapshotVersion);
    Task<ShipCard?> GetBySlugAsync(string publicSlug);

    /// <summary>The workspace's cards, newest version first.</summary>
    Task<List<ShipCard>> GetByWorkspaceIdAsync(Guid workspaceId);

    Task<bool> ExistsAsync(Guid workspaceId, int snapshotVersion);

    /// <summary>
    /// Saves everything tracked in this request. A second card for the same version, minted by a request that ran
    /// at the same time, fails here with <c>ShipAlreadyShippedException</c>.
    /// </summary>
    Task<bool> SaveChangesAsync();
}
