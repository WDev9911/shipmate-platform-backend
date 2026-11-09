using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class BuildBranchPolicyTests
{
    private static readonly Guid FeatureId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void FeatureBranch_IsRecognisedWithItsFeatureId()
    {
        var kind = BuildBranchPolicy.Classify($"feature/{FeatureId}-octocat", out var featureId);

        Assert.Equal(BuildBranchKind.Feature, kind);
        Assert.Equal(FeatureId, featureId);
    }

    [Fact]
    public void FeatureBranch_UsernameMayContainDashes()
    {
        var kind = BuildBranchPolicy.Classify($"feature/{FeatureId}-octo-cat", out var featureId);

        Assert.Equal(BuildBranchKind.Feature, kind);
        Assert.Equal(FeatureId, featureId);
    }

    [Theory]
    [InlineData("main")]
    [InlineData("develop")]
    [InlineData("release/1.0")]
    public void SharedBranches_AreProtected(string branch) =>
        Assert.Equal(BuildBranchKind.Protected, BuildBranchPolicy.Classify(branch, out _));

    [Theory]
    [InlineData("hotfix/42-octocat")]
    [InlineData("hotfix/")]
    public void HotfixBranches_AreRecognised(string branch) =>
        Assert.Equal(BuildBranchKind.Hotfix, BuildBranchPolicy.Classify(branch, out _));

    [Theory]
    [InlineData("my-branch")]
    [InlineData("feature/not-a-guid-octocat")]
    [InlineData("feature/11111111-1111-1111-1111-111111111111")]
    [InlineData("feature/11111111-1111-1111-1111-111111111111-")]
    [InlineData("feature/11111111-1111-1111-1111-111111111111octocat")]
    [InlineData("staging/11111111-1111-1111-1111-111111111111-octocat")]
    [InlineData("")]
    public void AnythingElse_IsInvalid(string branch) =>
        Assert.Equal(BuildBranchKind.Invalid, BuildBranchPolicy.Classify(branch, out _));

    [Fact]
    public void StagingBranch_MirrorsTheFeatureBranch() =>
        Assert.Equal(
            $"staging/{FeatureId}-octocat",
            BuildBranchPolicy.StagingBranchFor($"feature/{FeatureId}-octocat"));
}
