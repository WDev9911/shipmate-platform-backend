namespace ShipMate.Application.DTOs.Build;

public enum NextBestActionType
{
    // Your feature has an alert that blocks it: fix the code and submit again, or send a Feature Challenge.
    FixDrift,
    // A submission of yours passed the check and waits for you to approve it.
    ApproveSubmission,
    // Carry on with a feature of yours that nothing blocks.
    WorkOnFeature,
    // Nothing of yours to do: take a free feature that fits your skill.
    ClaimFeature,
    // The only work left is held up by another feature; BlockedReason says why.
    Blocked,
    // Nothing to suggest right now.
    NoAction
}

/// <summary>Why a feature cannot be worked on yet. The values are fixed by the spec.</summary>
public static class NextBestActionBlockedReasons
{
    // A feature it depends on is a backup nobody has taken yet.
    public const string DependencyPendingAssignment = "dependency_pending_assignment";

    // A feature it depends on has a drift alert nobody has dealt with.
    public const string DependencyDriftUnresolved = "dependency_drift_unresolved";
}

public class NextBestActionDto
{
    public NextBestActionType Action { get; set; }

    public Guid? FeatureId { get; set; }
    public string? FeatureName { get; set; }

    // FixDrift
    public Guid? AlertId { get; set; }

    // ApproveSubmission
    public Guid? SubmissionId { get; set; }

    // Blocked: why, and the feature it waits for.
    public string? BlockedReason { get; set; }
    public Guid? BlockedByFeatureId { get; set; }
}
