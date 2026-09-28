using ShipMate.Domain.Entities;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE step 5: when two duplicate features are merged, the one whose origin has the higher priority keeps
/// its content (so a committed feature always wins); on equal origins, the one listed first wins.
/// </summary>
public static class FeatureMergePolicy
{
    public static (Feature Survivor, Feature Absorbed) ChooseSurvivor(Feature first, Feature second)
    {
        if (first.Origin != second.Origin)
        {
            var winningOrigin = FeatureOriginPolicy.Resolve([first.Origin, second.Origin]);
            return first.Origin == winningOrigin ? (first, second) : (second, first);
        }

        return first.Position <= second.Position ? (first, second) : (second, first);
    }
}
