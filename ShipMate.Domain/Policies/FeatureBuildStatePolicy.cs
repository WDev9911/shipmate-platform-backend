using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// The BUILD state of one feature. "Approved" means the newest submission is approved, the same reading as the Next
/// Best Action and the VERIFY progress; "ready to approve" is decided by <see cref="BuildApprovalPolicy"/> itself.
/// </summary>
public static class FeatureBuildStatePolicy
{
    /// <param name="newest">The newest submission of the feature in any state, if any.</param>
    /// <param name="hasOpenAlert">An unresolved or acknowledged alert on the feature.</param>
    /// <param name="latestAlertStatus">Status of the feature's most recently raised alert, if any.</param>
    public static FeatureBuildState Evaluate(BuildSubmission? newest, bool hasOpenAlert, DriftAlertStatus? latestAlertStatus)
    {
        if (newest is null)
        {
            return FeatureBuildState.NotSubmitted;
        }

        switch (newest.Status)
        {
            case BuildSubmissionStatus.Approved:
                return FeatureBuildState.Approved;
            case BuildSubmissionStatus.Analyzing:
            // The newest can only be superseded while a newer submit is being saved: that one is on its way to analysis.
            case BuildSubmissionStatus.Superseded:
                return FeatureBuildState.Analyzing;
            case BuildSubmissionStatus.Failed:
                return FeatureBuildState.Failed;
        }

        return BuildApprovalPolicy.Evaluate(newest, newest, hasOpenAlert, latestAlertStatus) == ApprovalReadiness.Ready
            ? FeatureBuildState.ReadyToApprove
            : FeatureBuildState.DriftUnresolved;
    }
}
