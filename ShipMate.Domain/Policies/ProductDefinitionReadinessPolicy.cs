using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>What still blocks a product definition from READY_FOR_LOCK; ready when every list is empty.</summary>
public record ProductDefinitionReadiness(
    IReadOnlyList<Feature> PendingFeatures,
    IReadOnlyList<Feature> UnresolvedDuplicates,
    IReadOnlyList<Feature> UnresolvedPersonaConflicts,
    IReadOnlyList<Guid> DependencyCycle)
{
    public bool IsReady =>
        PendingFeatures.Count == 0
        && UnresolvedDuplicates.Count == 0
        && UnresolvedPersonaConflicts.Count == 0
        && DependencyCycle.Count == 0;
}

/// <summary>
/// DEFINE step 7: READY_FOR_LOCK requires no pending_confirmation feature, every possible_duplicate and
/// persona_conflict flag handled, and an acyclic depends_on graph.
/// </summary>
public static class ProductDefinitionReadinessPolicy
{
    public static ProductDefinitionReadiness Evaluate(ProductDefinition productDefinition)
    {
        var features = productDefinition.Features.OrderBy(f => f.Position).ToList();

        // Excluding a flagged feature is itself a way of handling the flag. The flag is kept, not cleared,
        // so it blocks again if the feature is ever included back.
        var activeFeatures = features.Where(f => f.Status != FeatureStatus.Excluded).ToList();

        return new ProductDefinitionReadiness(
            features.Where(f => f.Status == FeatureStatus.PendingConfirmation).ToList(),
            activeFeatures.Where(f => f.PossibleDuplicate).ToList(),
            activeFeatures.Where(f => f.PersonaConflict).ToList(),
            FeatureDependencyGraph.FindCycle(features));
    }
}
