namespace ShipMate.Domain.Policies;

public enum BuildBranchKind
{
    // feature/{featureId}-{username}
    Feature,
    // main, develop, release/*: shared branches, never submitted from
    Protected,
    Hotfix,
    // Anything else, or a feature branch without a valid feature id
    Invalid
}

/// <summary>BUILD: what a submitted branch name means.</summary>
public static class BuildBranchPolicy
{
    private const string FeaturePrefix = "feature/";
    private const string StagingPrefix = "staging/";
    private const int GuidLength = 36;

    public static BuildBranchKind Classify(string branchName, out Guid featureId)
    {
        featureId = Guid.Empty;

        if (branchName is "main" or "develop" || branchName.StartsWith("release/", StringComparison.Ordinal))
        {
            return BuildBranchKind.Protected;
        }

        if (branchName.StartsWith("hotfix/", StringComparison.Ordinal))
        {
            return BuildBranchKind.Hotfix;
        }

        // feature/{36-char guid}-{username}
        var rest = branchName.StartsWith(FeaturePrefix, StringComparison.Ordinal)
            ? branchName[FeaturePrefix.Length..]
            : string.Empty;
        var hasUsername = rest.Length > GuidLength + 1 && rest[GuidLength] == '-';

        return hasUsername && Guid.TryParse(rest[..GuidLength], out featureId)
            ? BuildBranchKind.Feature
            : BuildBranchKind.Invalid;
    }

    // The developer pushes to staging/{featureId}-{username}; ShipMate later moves it to feature/...
    public static string StagingBranchFor(string featureBranch) =>
        StagingPrefix + featureBranch[FeaturePrefix.Length..];
}
