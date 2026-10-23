using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class FeatureMergeAndChangePolicyTests
{
    [Fact]
    public void ChooseSurvivor_CommittedFeatureAlwaysWins()
    {
        var mentioned = TestData.Feature("mentioned", FeatureOrigin.FromCustomerMentioned, position: 0);
        var committed = TestData.Feature("committed", FeatureOrigin.FromCommittedList, position: 5);

        var (survivor, absorbed) = FeatureMergePolicy.ChooseSurvivor(mentioned, committed);

        Assert.Same(committed, survivor);
        Assert.Same(mentioned, absorbed);
    }

    [Fact]
    public void ChooseSurvivor_HigherOriginWinsRegardlessOfArgumentOrder()
    {
        var ai = TestData.Feature("ai", FeatureOrigin.FromAi);
        var mentioned = TestData.Feature("mentioned", FeatureOrigin.FromCustomerMentioned);

        Assert.Same(mentioned, FeatureMergePolicy.ChooseSurvivor(ai, mentioned).Survivor);
        Assert.Same(mentioned, FeatureMergePolicy.ChooseSurvivor(mentioned, ai).Survivor);
    }

    [Fact]
    public void ChooseSurvivor_EqualOrigin_EarlierPositionWins()
    {
        var first = TestData.Feature("first", position: 1);
        var second = TestData.Feature("second", position: 2);

        Assert.Same(first, FeatureMergePolicy.ChooseSurvivor(second, first).Survivor);
        Assert.Same(first, FeatureMergePolicy.ChooseSurvivor(first, second).Survivor);
    }

    [Theory]
    [InlineData(FeatureOrigin.FromCommittedList, FeatureStatus.Excluded, true)]
    [InlineData(FeatureOrigin.FromCommittedList, FeatureStatus.Included, false)]
    [InlineData(FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, false)]
    [InlineData(FeatureOrigin.FromCustomerMentioned, FeatureStatus.Excluded, false)]
    [InlineData(FeatureOrigin.FromAi, FeatureStatus.Excluded, false)]
    public void StatusChange_OnlyExcludingCommittedNeedsChangeRequest(FeatureOrigin origin, FeatureStatus target, bool expected)
    {
        Assert.Equal(expected, FeatureChangePolicy.RequiresChangeRequestForStatus(origin, target));
    }

    [Theory]
    [InlineData(FeatureOrigin.FromCommittedList, true)]
    [InlineData(FeatureOrigin.FromCustomerMentioned, false)]
    [InlineData(FeatureOrigin.FromAi, false)]
    public void Edit_OnlyCommittedNeedsChangeRequest(FeatureOrigin origin, bool expected)
    {
        Assert.Equal(expected, FeatureChangePolicy.RequiresChangeRequestForEdit(origin));
    }

    [Theory]
    [InlineData(FeatureOrigin.FromCommittedList, FeatureStatus.Excluded, FeatureStatus.Included, true)]
    [InlineData(FeatureOrigin.FromCommittedList, FeatureStatus.PendingConfirmation, FeatureStatus.Included, false)]
    [InlineData(FeatureOrigin.FromCommittedList, FeatureStatus.Included, FeatureStatus.Included, false)]
    [InlineData(FeatureOrigin.FromAi, FeatureStatus.Excluded, FeatureStatus.Included, false)]
    public void Include_OnlyReIncludingExcludedCommittedIsAudited(
        FeatureOrigin origin, FeatureStatus previous, FeatureStatus next, bool expected)
    {
        Assert.Equal(expected, FeatureChangePolicy.IsAuditedStatusChange(origin, previous, next));
    }
}
