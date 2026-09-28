using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

public interface IProductDefinitionService
{
    Task<ProductDefinitionDto> GetAsync(Guid userId, Guid workspaceId);

    Task<List<FeatureChangeLogDto>> GetFeatureChangeHistoryAsync(Guid userId, Guid workspaceId, Guid featureId);

    /// <summary>Moves the product definition to READY_FOR_LOCK once every DEFINE step 7 condition holds.</summary>
    Task<ProductDefinitionDto> MarkReadyForLockAsync(Guid userId, Guid workspaceId);

    Task<ProductDefinitionReportDto> GetReportAsync(Guid userId, Guid workspaceId);

    Task<FeatureDto> AddCommittedFeatureAsync(Guid userId, Guid workspaceId, CreateCommittedFeatureRequest request);
    Task<FeatureDto> UpdateCommittedFeatureAsync(
        Guid userId, Guid workspaceId, Guid featureId, UpdateCommittedFeatureRequest request);
    Task DeleteCommittedFeatureAsync(Guid userId, Guid workspaceId, Guid featureId);
}
