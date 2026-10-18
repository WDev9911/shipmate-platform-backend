using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class FeatureListPolicyTests
{
    [Fact]
    public void KillList_ContainsExcludedAndPendingFeaturesTheAiWantsCut()
    {
        var excluded = TestData.Feature(status: FeatureStatus.Excluded, verdict: AiVerdict.NotRequired);
        var pendingCut = TestData.Feature(
            origin: FeatureOrigin.FromCustomerMentioned, status: FeatureStatus.PendingConfirmation, verdict: AiVerdict.NotRequired);
        var pendingKept = TestData.Feature(
            origin: FeatureOrigin.FromCustomerMentioned, status: FeatureStatus.PendingConfirmation, verdict: AiVerdict.Required);
        var included = TestData.Feature(status: FeatureStatus.Included);

        Assert.True(KillListPolicy.IsInKillList(excluded));
        Assert.True(KillListPolicy.IsInKillList(pendingCut));
        Assert.False(KillListPolicy.IsInKillList(pendingKept));
        Assert.False(KillListPolicy.IsInKillList(included));
    }

    [Fact]
    public void CoreFeatureCount_CountsOnlyNonExcludedPrimaryFeatures()
    {
        var features = new[]
        {
            TestData.Feature("p1"),
            TestData.Feature("p2", status: FeatureStatus.PendingConfirmation),
            TestData.Feature("p3-excluded", status: FeatureStatus.Excluded),
            TestData.Feature("s1", role: FeatureRole.Supporting),
            TestData.Feature("s2-excluded", role: FeatureRole.Supporting, status: FeatureStatus.Excluded)
        };

        Assert.Equal(2, CoreFeatureRecommendationPolicy.CountCoreFeatures(features));
        Assert.Equal(1, CoreFeatureRecommendationPolicy.CountSupportingFeatures(features));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void ExceedsRecommendation_OnlyAboveFive(int count, bool expected)
    {
        Assert.Equal(expected, CoreFeatureRecommendationPolicy.ExceedsRecommendation(count));
    }

    [Theory]
    [InlineData(FeatureOrigin.FromAi, false, true)]
    [InlineData(FeatureOrigin.FromAi, true, false)]
    [InlineData(FeatureOrigin.FromCustomerMentioned, false, false)]
    [InlineData(FeatureOrigin.FromCommittedList, false, false)]
    public void CanRegenerate_OnlyUndecidedAiFeatures(FeatureOrigin origin, bool devDecided, bool expected)
    {
        var feature = TestData.Feature(origin: origin);
        feature.DevDecided = devDecided;

        Assert.Equal(expected, FeatureRegenerationPolicy.CanRegenerate(feature));
    }
}
