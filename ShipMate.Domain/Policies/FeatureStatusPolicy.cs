using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// DEFINE step 7: the default status a feature gets, decided only by its origin and the AI verdict.
/// </summary>
public static class FeatureStatusPolicy
{
    public static FeatureStatus DefaultFor(FeatureOrigin origin, AiVerdict? verdict) => origin switch
    {
        FeatureOrigin.FromAi => verdict switch
        {
            AiVerdict.Required => FeatureStatus.Included,
            AiVerdict.NotRequired => FeatureStatus.Excluded,
            _ => throw new ArgumentException("An AI-originated feature must have an AI verdict.", nameof(verdict))
        },
        FeatureOrigin.FromCustomerMentioned => FeatureStatus.PendingConfirmation,
        FeatureOrigin.FromCommittedList => FeatureStatus.PendingConfirmation,
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
    };
}
