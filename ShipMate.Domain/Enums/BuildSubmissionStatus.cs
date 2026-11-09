namespace ShipMate.Domain.Enums;

public enum BuildSubmissionStatus
{
    // Accepted by ShipMate and waiting for (or going through) the scope analysis.
    Analyzing,
    // A newer submit of the same feature replaced this one before its analysis finished.
    Superseded,
    // The code stays inside the locked scope (confidence at or above the threshold).
    InScope,
    // The code goes beyond the locked scope (confidence below the threshold).
    Drift,
    // Only maintenance changes since the last analysis, so the AI was skipped and the previous verdict stands.
    SkippedMaintenance,
    // The developer approved it: the Bot moved the feature branch to the checked commit.
    Approved,
    // The analysis couldn't produce a verdict; FailureCode says why.
    Failed
}
