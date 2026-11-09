namespace ShipMate.Domain.Enums;

/// <summary>Why a submission was judged to go beyond the locked scope.</summary>
public enum DriftTrigger
{
    // The AI listed code outside the feature, whatever score it gave.
    Findings,
    // The score is below the workspace's threshold and the AI listed nothing.
    LowConfidence,
    // Both.
    Both
}
