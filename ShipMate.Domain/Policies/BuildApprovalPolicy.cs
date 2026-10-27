using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

public enum ApprovalReadiness
{
    Ready,
    // A newer submission of the feature replaced it.
    Superseded,
    // Still being analysed, failed, or already approved.
    NotReady,
    // The code goes beyond the locked scope and no Manager has overridden the alert.
    DriftUnresolved
}

/// <summary>Whether a submission may be approved. Shared by the approval itself and by the Next Best Action.</summary>
public static class BuildApprovalPolicy
{
    /// <param name="newest">The newest submission of the same feature in any state.</param>
    /// <param name="hasOpenAlert">An unresolved or acknowledged alert on the feature.</param>
    /// <param name="latestAlertStatus">Status of the feature's most recently raised alert, if any.</param>
    public static ApprovalReadiness Evaluate(
        BuildSubmission submission, BuildSubmission? newest, bool hasOpenAlert, DriftAlertStatus? latestAlertStatus)
    {
        if ((newest is not null && newest.Id != submission.Id) || submission.Status == BuildSubmissionStatus.Superseded)
        {
            return ApprovalReadiness.Superseded;
        }

        if (submission.Status is not (BuildSubmissionStatus.InScope or BuildSubmissionStatus.Drift
            or BuildSubmissionStatus.SkippedMaintenance))
        {
            return ApprovalReadiness.NotReady;
        }

        if (hasOpenAlert)
        {
            return ApprovalReadiness.DriftUnresolved;
        }

        // A verdict below the threshold needs a Manager's override. Maintenance-only submissions repeat the earlier
        // verdict, so the alert that stopped that one is the one that must have been overridden.
        var isDrift = submission.ConfidenceScore < submission.ThresholdAtCheck;
        if (isDrift && latestAlertStatus != DriftAlertStatus.Overridden)
        {
            return ApprovalReadiness.DriftUnresolved;
        }

        return ApprovalReadiness.Ready;
    }
}
