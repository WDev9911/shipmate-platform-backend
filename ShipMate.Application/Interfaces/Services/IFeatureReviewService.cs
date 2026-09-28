using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>The developer's review actions on an analyzed product definition (DEFINE step 5).</summary>
public interface IFeatureReviewService
{
    Task<ProductDefinitionDto> ChangeStatusAsync(
        Guid userId, Guid workspaceId, Guid featureId, UpdateFeatureStatusRequest request);

    Task<FeatureDto> EditAsync(Guid userId, Guid workspaceId, Guid featureId, UpdateFeatureRequest request);

    /// <summary>Merges a feature flagged as a possible duplicate with the feature it duplicates.</summary>
    Task<ProductDefinitionDto> MergeAsync(Guid userId, Guid workspaceId, Guid featureId);

    /// <summary>Resolves a possible-duplicate flag by keeping both features as distinct.</summary>
    Task<ProductDefinitionDto> KeepSeparateAsync(Guid userId, Guid workspaceId, Guid featureId);

    /// <summary>Resolves a persona-conflict flag by keeping the feature as a reasonable exception.</summary>
    Task<FeatureDto> AcceptPersonaConflictAsync(Guid userId, Guid workspaceId, Guid featureId);

    Task<FeatureDto> AddDependencyAsync(
        Guid userId, Guid workspaceId, Guid featureId, AddFeatureDependencyRequest request);

    Task RemoveDependencyAsync(Guid userId, Guid workspaceId, Guid featureId, Guid dependsOnFeatureId);
}
