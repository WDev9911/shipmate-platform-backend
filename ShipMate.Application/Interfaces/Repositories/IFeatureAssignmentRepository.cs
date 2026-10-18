using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IFeatureAssignmentRepository
{
    Task<List<FeatureAssignment>> GetByWorkspaceIdAsync(Guid workspaceId);

    /// <summary>
    /// Saves the assignment, or returns false when the feature already has one. Atomic in the database,
    /// so of several simultaneous claims exactly one wins.
    /// </summary>
    Task<bool> TryAddAsync(FeatureAssignment assignment);

    Task<bool> SaveChangesAsync();
}
