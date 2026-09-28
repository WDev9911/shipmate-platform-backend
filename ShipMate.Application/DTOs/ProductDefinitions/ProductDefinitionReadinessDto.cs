using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class ProductDefinitionReadinessDto
{
    public bool IsReady { get; set; }
    public List<FeatureReferenceDto> PendingFeatures { get; set; } = new();
    public List<FeatureReferenceDto> UnresolvedDuplicates { get; set; } = new();
    public List<FeatureReferenceDto> UnresolvedPersonaConflicts { get; set; } = new();

    // In dependency order, starting and ending with the same feature; empty when there is no cycle.
    public List<FeatureReferenceDto> DependencyCycle { get; set; } = new();

    public static ProductDefinitionReadinessDto From(ProductDefinitionReadiness readiness, IEnumerable<Feature> features)
    {
        var namesById = features.ToDictionary(f => f.Id, f => f.Name);

        return new ProductDefinitionReadinessDto
        {
            IsReady = readiness.IsReady,
            PendingFeatures = ToReferences(readiness.PendingFeatures),
            UnresolvedDuplicates = ToReferences(readiness.UnresolvedDuplicates),
            UnresolvedPersonaConflicts = ToReferences(readiness.UnresolvedPersonaConflicts),
            DependencyCycle = readiness.DependencyCycle.Select(id => new FeatureReferenceDto(id, namesById[id])).ToList()
        };
    }

    private static List<FeatureReferenceDto> ToReferences(IEnumerable<Feature> features) =>
        features.Select(f => new FeatureReferenceDto(f.Id, f.Name)).ToList();
}
