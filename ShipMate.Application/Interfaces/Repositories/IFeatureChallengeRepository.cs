using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IFeatureChallengeRepository
{
    Task<FeatureChallenge?> GetByIdAsync(Guid workspaceId, Guid challengeId);

    /// <summary>The workspace's challenges, newest first.</summary>
    Task<List<FeatureChallenge>> GetByWorkspaceIdAsync(Guid workspaceId);

    Task AddAsync(FeatureChallenge challenge);

    // Saves everything tracked in this request, including the snapshot a decision creates.
    Task<bool> SaveChangesAsync();
}
