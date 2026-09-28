using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

public interface IProductDefinitionService
{
    Task<ProductDefinitionDto> GetAsync(Guid userId, Guid workspaceId);

    Task<FeatureDto> AddCommittedFeatureAsync(Guid userId, Guid workspaceId, CreateCommittedFeatureRequest request);
    Task<FeatureDto> UpdateCommittedFeatureAsync(
        Guid userId, Guid workspaceId, Guid featureId, UpdateCommittedFeatureRequest request);
    Task DeleteCommittedFeatureAsync(Guid userId, Guid workspaceId, Guid featureId);
}
