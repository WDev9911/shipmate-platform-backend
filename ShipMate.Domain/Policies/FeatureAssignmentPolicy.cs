namespace ShipMate.Domain.Policies;

/// <summary>LOCK Feature Assignment: who may take a feature, and the branch name they get.</summary>
public static class FeatureAssignmentPolicy
{
    // A developer is qualified when their declared skill reaches the feature's complexity.
    public static bool IsQualified(int? skillLevel, int complexity) => skillLevel >= complexity;

    public static string BranchNameFor(Guid featureId, string githubUsername) =>
        $"feature/{featureId}-{githubUsername}";
}
