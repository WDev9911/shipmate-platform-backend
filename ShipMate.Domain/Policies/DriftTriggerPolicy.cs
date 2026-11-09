using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

public static class DriftTriggerPolicy
{
    /// <summary>
    /// The reason a drift was raised. A drift always has one of the two: the caller passes what the alert was raised with,
    /// so a submission that was not a drift at all is reported as LowConfidence rather than invented a reason.
    /// </summary>
    public static DriftTrigger Of(decimal confidenceScore, decimal threshold, bool hasFindings)
    {
        var lowConfidence = confidenceScore < threshold;
        return hasFindings
            ? (lowConfidence ? DriftTrigger.Both : DriftTrigger.Findings)
            : DriftTrigger.LowConfidence;
    }
}
