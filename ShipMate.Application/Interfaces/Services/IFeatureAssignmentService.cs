using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>LOCK Feature Assignment: developers see which locked features they qualify for and take one.</summary>
public interface IFeatureAssignmentService
{
    Task<List<FeatureAssignmentOverviewDto>> GetOverviewAsync(Guid userId, Guid workspaceId);

    /// <summary>First come, first served: the first developer to claim a feature gets it.</summary>
    Task<FeatureAssignmentDto> ClaimAsync(Guid userId, Guid workspaceId, Guid featureId);

    /// <summary>The developer holding the feature retries creating its branch on GitHub.</summary>
    Task<FeatureAssignmentDto> RetryBranchAsync(Guid userId, Guid workspaceId, Guid featureId);
}
