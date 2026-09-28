using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

public interface IProductDefinitionAnalysisService
{
    /// <summary>
    /// Runs the AI analysis of the workspace's product definition (DEFINE steps 2-4). Every run after the first
    /// is a re-analysis that keeps the developer's decisions (step 5b).
    /// </summary>
    Task<AnalyzeProductDefinitionResponse> AnalyzeAsync(
        Guid userId, Guid workspaceId, AnalyzeProductDefinitionRequest? request, CancellationToken cancellationToken = default);
}
