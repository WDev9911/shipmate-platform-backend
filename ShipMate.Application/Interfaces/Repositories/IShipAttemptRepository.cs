using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IShipAttemptRepository
{
    Task AddAsync(ShipAttempt attempt);
    Task<ShipAttempt?> GetByIdAsync(Guid workspaceId, Guid attemptId);

    /// <summary>The workspace's most recent attempts, newest first.</summary>
    Task<List<ShipAttempt>> GetRecentAsync(Guid workspaceId, int limit);

    /// <summary>When each attempt of the workspace made after <paramref name="since"/> was started.</summary>
    Task<List<DateTime>> GetStartTimesSinceAsync(Guid workspaceId, DateTime since);

    Task<bool> SaveChangesAsync();
}
