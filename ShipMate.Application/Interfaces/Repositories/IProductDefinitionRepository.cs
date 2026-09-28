using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IProductDefinitionRepository
{
    /// <summary>The workspace's product definition with its features and their dependencies loaded.</summary>
    Task<ProductDefinition?> GetByWorkspaceIdAsync(Guid workspaceId);

    Task<bool> HasSuccessfulAnalysisRunAsync(Guid productDefinitionId);
    Task AddAsync(ProductDefinition productDefinition);
    Task AddFeatureAsync(Feature feature);
    void RemoveFeature(Feature feature);
    Task<bool> SaveChangesAsync();
}
