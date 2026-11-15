namespace ShipMate.Domain.Enums;

/// <summary>Where a feature stands in BUILD, read from its newest submission and its drift alerts.</summary>
public enum FeatureBuildState
{
    // Nothing submitted for the feature yet.
    NotSubmitted,
    // The newest submission is waiting for or going through the scope analysis.
    Analyzing,
    // The analysis of the newest submission couldn't reach a verdict; resubmit.
    Failed,
    // The code goes beyond the locked scope and nobody has dealt with it yet.
    DriftUnresolved,
    // The newest submission passed the check and waits for its developer to approve it.
    ReadyToApprove,
    // The newest submission was approved: the Bot moved the feature branch to it. Not the same as merged into main.
    Approved
}
