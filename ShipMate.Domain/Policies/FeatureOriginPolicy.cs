using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE step 6: a feature's origin is the highest-priority value among its sources,
/// ranked from_committed_list > from_customer_mentioned > from_ai.
/// </summary>
public static class FeatureOriginPolicy
{
    private static readonly IReadOnlyList<FeatureOrigin> PriorityHighestFirst =
    [
        FeatureOrigin.FromCommittedList,
        FeatureOrigin.FromCustomerMentioned,
        FeatureOrigin.FromAi
    ];

    public static FeatureOrigin Resolve(IEnumerable<FeatureOrigin> sources)
    {
        var sourceSet = sources.ToHashSet();
        if (sourceSet.Count == 0)
        {
            throw new ArgumentException("A feature must have at least one source.", nameof(sources));
        }

        return PriorityHighestFirst.First(sourceSet.Contains);
    }
}
