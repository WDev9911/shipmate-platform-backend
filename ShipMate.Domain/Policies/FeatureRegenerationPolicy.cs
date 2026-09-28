using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE step 5b: re-running the AI must never undo the developer's decisions. The AI may only regenerate
/// its own proposals the developer hasn't touched; everything else — every decided feature and every feature
/// that came from the developer's input — is kept.
/// </summary>
public static class FeatureRegenerationPolicy
{
    public static bool CanRegenerate(Feature feature) =>
        feature.Origin == FeatureOrigin.FromAi && !feature.DevDecided;
}
