using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE soft recommendation: an effective MVP usually has 3-5 core features. Only primary features that
/// are not excluded count; supporting features are reported separately. Never a hard limit.
/// </summary>
public static class CoreFeatureRecommendationPolicy
{
    public const int RecommendedMin = 3;
    public const int RecommendedMax = 5;

    public static int CountCoreFeatures(IEnumerable<Feature> features) =>
        features.Count(f => f.Role == FeatureRole.Primary && f.Status != FeatureStatus.Excluded);

    public static int CountSupportingFeatures(IEnumerable<Feature> features) =>
        features.Count(f => f.Role == FeatureRole.Supporting && f.Status != FeatureStatus.Excluded);

    public static bool ExceedsRecommendation(int coreFeatureCount) => coreFeatureCount > RecommendedMax;
}
