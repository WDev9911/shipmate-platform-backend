using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class FeatureStatusAndOriginPolicyTests
{
    [Theory]
    [InlineData(FeatureOrigin.FromAi, AiVerdict.Required, FeatureStatus.Included)]
    [InlineData(FeatureOrigin.FromAi, AiVerdict.NotRequired, FeatureStatus.Excluded)]
    [InlineData(FeatureOrigin.FromCustomerMentioned, AiVerdict.Required, FeatureStatus.PendingConfirmation)]
    [InlineData(FeatureOrigin.FromCustomerMentioned, AiVerdict.NotRequired, FeatureStatus.PendingConfirmation)]
    [InlineData(FeatureOrigin.FromCommittedList, AiVerdict.Required, FeatureStatus.PendingConfirmation)]
    [InlineData(FeatureOrigin.FromCommittedList, AiVerdict.NotRequired, FeatureStatus.PendingConfirmation)]
    public void DefaultStatus_FollowsStep7Table(FeatureOrigin origin, AiVerdict verdict, FeatureStatus expected)
    {
        Assert.Equal(expected, FeatureStatusPolicy.DefaultFor(origin, verdict));
    }

    [Fact]
    public void DefaultStatus_AiFeatureWithoutVerdict_Throws()
    {
        Assert.Throws<ArgumentException>(() => FeatureStatusPolicy.DefaultFor(FeatureOrigin.FromAi, null));
    }

    [Fact]
    public void DefaultStatus_CommittedFeatureWithoutVerdict_IsPending()
    {
        Assert.Equal(FeatureStatus.PendingConfirmation, FeatureStatusPolicy.DefaultFor(FeatureOrigin.FromCommittedList, null));
    }

    [Theory]
    [InlineData(FeatureOrigin.FromAi, FeatureOrigin.FromCustomerMentioned, FeatureOrigin.FromCustomerMentioned)]
    [InlineData(FeatureOrigin.FromAi, FeatureOrigin.FromCommittedList, FeatureOrigin.FromCommittedList)]
    [InlineData(FeatureOrigin.FromCustomerMentioned, FeatureOrigin.FromCommittedList, FeatureOrigin.FromCommittedList)]
    [InlineData(FeatureOrigin.FromAi, FeatureOrigin.FromAi, FeatureOrigin.FromAi)]
    public void ResolveOrigin_PicksHighestPriority(FeatureOrigin a, FeatureOrigin b, FeatureOrigin expected)
    {
        Assert.Equal(expected, FeatureOriginPolicy.Resolve([a, b]));
        Assert.Equal(expected, FeatureOriginPolicy.Resolve([b, a]));
    }

    [Fact]
    public void ResolveOrigin_NoSources_Throws()
    {
        Assert.Throws<ArgumentException>(() => FeatureOriginPolicy.Resolve([]));
    }
}
