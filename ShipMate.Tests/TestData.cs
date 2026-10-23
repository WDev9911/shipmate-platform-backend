using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests;

internal static class TestData
{
    public static Feature Feature(
        string name = "Feature",
        FeatureOrigin origin = FeatureOrigin.FromAi,
        FeatureStatus status = FeatureStatus.Included,
        FeatureRole role = FeatureRole.Primary,
        AiVerdict? verdict = AiVerdict.Required,
        int position = 0) => new()
    {
        Name = name,
        Description = $"{name} description",
        Origin = origin,
        Sources = [origin],
        Status = status,
        Role = role,
        AiVerdict = verdict,
        Position = position
    };

    // Reads as "feature depends on prerequisite".
    public static void DependsOn(Feature feature, Feature prerequisite) =>
        feature.Dependencies.Add(new FeatureDependency
        {
            FeatureId = feature.Id,
            DependsOnFeatureId = prerequisite.Id,
            DependsOnFeature = prerequisite
        });

    public static ProductDefinition Definition(params Feature[] features)
    {
        var definition = new ProductDefinition();
        foreach (var feature in features)
        {
            feature.ProductDefinitionId = definition.Id;
            definition.Features.Add(feature);
        }

        return definition;
    }
}
