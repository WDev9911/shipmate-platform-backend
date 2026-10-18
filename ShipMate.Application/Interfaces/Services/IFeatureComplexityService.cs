using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>LOCK step 1: every included feature gets a 1-5 complexity score. The AI proposes, the Manager confirms.</summary>
public interface IFeatureComplexityService
{
    /// <summary>Asks the AI to score every included feature and stores the result as the proposal.</summary>
    Task<ProductDefinitionDto> ProposeAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>Stores the Manager's confirmed scores for the given included features.</summary>
    Task<ProductDefinitionDto> SetAsync(Guid userId, Guid workspaceId, SetComplexityRequest request);
}
