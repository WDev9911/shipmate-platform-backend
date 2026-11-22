using ShipMate.Domain.Policies;
using Keys = ShipMate.Domain.Policies.ScopeDisciplinePolicy.Keys;

namespace ShipMate.Tests.Domain;

public class ScopeDisciplinePolicyTests
{
    private static ScopeDisciplineCounts Counts(
        int scopeChanges = 0, int removeAndReplace = 0, int addDirectly = 0, int bypasses = 0,
        int dilutions = 0, int openDrift = 0, int daysLate = 0, int directPushes = 0) =>
        new(scopeChanges, removeAndReplace, addDirectly, bypasses, dilutions, openDrift, daysLate, directPushes);

    private static ScoreComponent Part(ScopeDisciplineScore score, string key) => score.Components.Single(c => c.Key == key);

    [Fact]
    public void NothingHappened_IsAFullScore_AndEveryPartIsListed()
    {
        var score = ScopeDisciplinePolicy.Compute(Counts());

        Assert.Equal(100, score.Score);
        Assert.Equal(ScopeDisciplinePolicy.FormulaVersion, score.FormulaVersion);
        Assert.Equal(
            [Keys.ScopeVolatility, Keys.ScopeBypass, Keys.GovernanceDilution, Keys.OpenDrift, Keys.RemoveAndReplace, Keys.DeadlineOverrun, Keys.DirectPush],
            score.Components.Select(c => c.Key));
        Assert.All(score.Components, c => Assert.Equal(0, c.Deduction));
    }

    [Fact]
    public void EachPart_TakesOffItsOwnAmount()
    {
        var score = ScopeDisciplinePolicy.Compute(Counts(
            scopeChanges: 2, removeAndReplace: 1, addDirectly: 1, bypasses: 2, dilutions: 1, openDrift: 2, daysLate: 3, directPushes: 1));

        Assert.Equal(6, Part(score, Keys.ScopeVolatility).Deduction);
        Assert.Equal(10, Part(score, Keys.ScopeBypass).Deduction);
        Assert.Equal(10, Part(score, Keys.GovernanceDilution).Deduction);
        Assert.Equal(10, Part(score, Keys.OpenDrift).Deduction);
        Assert.Equal(5, Part(score, Keys.RemoveAndReplace).Deduction);
        Assert.Equal(3, Part(score, Keys.DeadlineOverrun).Deduction);
        Assert.Equal(5, Part(score, Keys.DirectPush).Deduction);
        Assert.Equal(100 - 49, score.Score);
    }

    [Fact]
    public void APart_NeverTakesMoreThanItsCap()
    {
        var score = ScopeDisciplinePolicy.Compute(Counts(bypasses: 40));

        var bypass = Part(score, Keys.ScopeBypass);
        Assert.Equal(40, bypass.Count);
        Assert.Equal(25, bypass.Deduction);
        Assert.Equal(25, bypass.Cap);
        Assert.Equal(75, score.Score);
    }

    [Fact]
    public void EveryPartAtItsCap_StopsAtZero_NotBelow()
    {
        var score = ScopeDisciplinePolicy.Compute(Counts(
            scopeChanges: 99, removeAndReplace: 99, bypasses: 99, dilutions: 99, openDrift: 99, daysLate: 999, directPushes: 99));

        Assert.Equal(0, score.Score);
    }

    [Fact]
    public void AHugeCount_DoesNotOverflow()
    {
        var score = ScopeDisciplinePolicy.Compute(Counts(daysLate: int.MaxValue));

        Assert.Equal(15, Part(score, Keys.DeadlineOverrun).Deduction);
    }

    [Theory]
    [InlineData(0, 0, 0)]  // no scope change: nothing to share
    [InlineData(0, 3, 0)]  // only additions
    [InlineData(1, 1, 5)]  // half
    [InlineData(1, 2, 3)]  // 3.33 rounds down
    [InlineData(2, 1, 7)]  // 6.67 rounds up
    [InlineData(4, 0, 10)] // every change removed a feature
    public void RemoveAndReplace_IsAShareOfTheChanges(int removed, int added, int expected)
    {
        var part = Part(ScopeDisciplinePolicy.Compute(Counts(removeAndReplace: removed, addDirectly: added)), Keys.RemoveAndReplace);

        Assert.Equal(expected, part.Deduction);
        Assert.Equal(removed + added, part.OutOf);
    }

    [Fact]
    public void ANegativeCount_IsTreatedAsNone()
    {
        var score = ScopeDisciplinePolicy.Compute(Counts(bypasses: -3, removeAndReplace: -1, addDirectly: -1));

        Assert.Equal(100, score.Score);
    }

    [Fact]
    public void DaysLate_WithoutDeadline_IsZero()
    {
        Assert.Equal(0, ScopeDisciplinePolicy.DaysLate(null, DateTime.UtcNow));
    }

    [Fact]
    public void DaysLate_OnTheDeadlineDay_IsOnTime_WhateverTheHour()
    {
        var deadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(0, ScopeDisciplinePolicy.DaysLate(deadline, deadline.AddHours(23).AddMinutes(59)));
        Assert.Equal(0, ScopeDisciplinePolicy.DaysLate(deadline, deadline.AddDays(-5)));
    }

    [Fact]
    public void DaysLate_CountsWholeDaysAfterTheDeadlineDay()
    {
        var deadline = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, ScopeDisciplinePolicy.DaysLate(deadline, deadline.AddDays(1).AddMinutes(1)));
        Assert.Equal(3, ScopeDisciplinePolicy.DaysLate(deadline, deadline.AddDays(3).AddHours(12)));
    }
}
