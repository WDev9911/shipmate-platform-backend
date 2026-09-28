using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE step 7: the kill list is every excluded feature, plus every feature still pending confirmation
/// that the AI proposed to cut. It is derived from the features, never stored separately.
/// </summary>
public static class KillListPolicy
{
    public static bool IsInKillList(Feature feature) =>
        feature.Status == FeatureStatus.Excluded
        || (feature.Status == FeatureStatus.PendingConfirmation && feature.AiVerdict == AiVerdict.NotRequired);
}
