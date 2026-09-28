using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

public interface IProductDefinitionAnalysisService
{
    /// <summary>Runs the first AI analysis of the workspace's product definition (DEFINE steps 2-4).</summary>
    Task<ProductDefinitionDto> AnalyzeAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default);
}
