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
    Task AddDependencyAsync(FeatureDependency dependency);
    void RemoveDependency(FeatureDependency dependency);
    Task AddAnalysisRunAsync(AiAnalysisRun run);
    Task AddChangeLogAsync(FeatureChangeLog changeLog);

    /// <summary>A feature's change history, newest first, with the user who made each change loaded.</summary>
    Task<List<FeatureChangeLog>> GetChangeLogsAsync(Guid featureId);
    Task<bool> SaveChangesAsync();
}
