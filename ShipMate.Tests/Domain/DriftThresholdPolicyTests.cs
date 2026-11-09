using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class DriftThresholdPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime[] None = [];

    private static ThresholdChangeVerdict Check(decimal requested, params DateTime[] previous) =>
        DriftThresholdPolicy.Check(0.75m, requested, previous, Now);

    // ---- range ----

    [Theory]
    [InlineData(0.70)]
    [InlineData(0.80)]
    [InlineData(0.85)]
    public void AValueInRange_IsAllowed(double value) =>
        Assert.Equal(ThresholdChangeVerdict.Allowed, Check((decimal)value));

    [Theory]
    [InlineData(0.69)]
    [InlineData(0.65)]
    [InlineData(0.50)]
    [InlineData(0.86)]
    [InlineData(1.00)]
    [InlineData(0.00)]
    public void AValueOutOfRange_IsRefused(double value) =>
        Assert.Equal(ThresholdChangeVerdict.OutOfRange, Check((decimal)value));

    [Fact]
    public void TheCurrentValue_IsNotAChange() =>
        Assert.Equal(ThresholdChangeVerdict.Unchanged, Check(0.75m));

    [Fact]
    public void OutOfRange_IsReportedBeforeUnchanged() =>
        Assert.Equal(ThresholdChangeVerdict.OutOfRange,
            DriftThresholdPolicy.Check(0.50m, 0.50m, None, Now));

    // ---- how often ----

    [Fact]
    public void TheFirstChange_NeedsNoWaiting() =>
        Assert.Equal(ThresholdChangeVerdict.Allowed, Check(0.80m));

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void ASecondChangeWithinSevenDays_IsRefused(int daysAgo) =>
        Assert.Equal(ThresholdChangeVerdict.CoolingDown, Check(0.80m, Now.AddDays(-daysAgo)));

    [Fact]
    public void ASecondChangeExactlySevenDaysLater_IsAllowed() =>
        Assert.Equal(ThresholdChangeVerdict.Allowed, Check(0.80m, Now.AddDays(-7)));

    [Fact]
    public void TheWaitCountsFromTheLatestChange() =>
        Assert.Equal(ThresholdChangeVerdict.CoolingDown, Check(0.80m, Now.AddDays(-30), Now.AddDays(-2)));

    [Fact]
    public void ARaiseUsesTheSameAllowanceAsALowering()
    {
        // The last change was a raise to 0.80; lowering back soon is just as refused.
        var verdict = DriftThresholdPolicy.Check(0.80m, 0.70m, [Now.AddDays(-1)], Now);

        Assert.Equal(ThresholdChangeVerdict.CoolingDown, verdict);
    }

    // ---- how many ----

    [Fact]
    public void TwoEarlierChanges_LeaveOneMore() =>
        Assert.Equal(ThresholdChangeVerdict.Allowed, Check(0.80m, Now.AddDays(-30), Now.AddDays(-20)));

    [Fact]
    public void ThreeEarlierChanges_UseThemAllUp() =>
        Assert.Equal(ThresholdChangeVerdict.LimitReached,
            Check(0.80m, Now.AddDays(-60), Now.AddDays(-40), Now.AddDays(-20)));

    [Fact]
    public void TheLimit_IsReportedBeforeTheWait() =>
        Assert.Equal(ThresholdChangeVerdict.LimitReached,
            Check(0.80m, Now.AddDays(-60), Now.AddDays(-40), Now.AddDays(-1)));

    // ---- when next ----

    [Fact]
    public void NextAllowedChange_IsSevenDaysAfterTheLatestOne() =>
        Assert.Equal(Now.AddDays(-2).AddDays(7),
            DriftThresholdPolicy.NextAllowedChange([Now.AddDays(-30), Now.AddDays(-2)]));
}
