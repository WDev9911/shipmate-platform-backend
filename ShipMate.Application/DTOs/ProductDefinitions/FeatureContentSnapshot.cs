using System.Text.Json;
using System.Text.Json.Serialization;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>The content of a feature at one point in time, stored as JSON in change_history (DEFINE step 8).</summary>
public record FeatureContentSnapshot(
    string Name,
    string Description,
    string? Scope,
    FeatureOrigin Origin,
    IReadOnlyList<FeatureOrigin> Sources,
    FeatureStatus Status)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static FeatureContentSnapshot Of(Feature feature) =>
        new(feature.Name, feature.Description, feature.Scope, feature.Origin, feature.Sources.ToList(), feature.Status);

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static string ToJson(IEnumerable<FeatureContentSnapshot> snapshots) =>
        JsonSerializer.Serialize(snapshots, JsonOptions);
}
