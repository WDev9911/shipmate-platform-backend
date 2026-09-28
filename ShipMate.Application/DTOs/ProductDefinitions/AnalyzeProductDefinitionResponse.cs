namespace ShipMate.Application.DTOs.ProductDefinitions;

public class AnalyzeProductDefinitionResponse
{
    public ProductDefinitionDto ProductDefinition { get; set; } = new();

    // Warnings for the developer; always empty on a first analysis.
    public List<RemovedDependencyDto> RemovedDependencies { get; set; } = new();
}
