using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Validators.Admin;

namespace ShipMate.Tests.Application;

public class PlatformSettingsValidatorTests
{
    private readonly UpdatePlatformSettingsRequestValidator _update = new();

    private static UpdatePlatformSettingsRequest Valid() => new()
    {
        ComplexityBudgetPerDeveloper = 10,
        DefaultDriftThreshold = 0.75m,
        MaintenanceCommitPatterns = ["fix typo", "refactor"],
        Revision = 0
    };

    // ---------- Updating ----------

    [Fact]
    public void Update_TypicalValues_AreValid() => Assert.True(_update.Validate(Valid()).IsValid);

    [Theory]
    [InlineData(5, true)]
    [InlineData(50, true)]
    [InlineData(4, false)]
    [InlineData(51, false)]
    [InlineData(0, false)]
    [InlineData(-10, false)]
    public void Update_BudgetPerDeveloper_StaysBetween5And50(int budget, bool valid)
    {
        var request = Valid();
        request.ComplexityBudgetPerDeveloper = budget;

        Assert.Equal(valid, _update.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("0.70", true)]
    [InlineData("0.85", true)]
    [InlineData("0.7", true)]
    [InlineData("0.755", false)]
    [InlineData("0.69", false)]
    [InlineData("0.86", false)]
    [InlineData("0.65", false)]
    [InlineData("0", false)]
    [InlineData("1", false)]
    public void Update_Threshold_StaysInTheManagerRangeWithTwoDecimals(string threshold, bool valid)
    {
        var request = Valid();
        request.DefaultDriftThreshold = decimal.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(valid, _update.Validate(request).IsValid);
    }

    [Fact]
    public void Update_NegativeRevision_IsInvalid()
    {
        var request = Valid();
        request.Revision = -1;

        Assert.False(_update.Validate(request).IsValid);
    }

    [Fact]
    public void Update_Patterns_BetweenOneAndThirty()
    {
        var request = Valid();
        request.MaintenanceCommitPatterns = [];
        Assert.False(_update.Validate(request).IsValid);

        request.MaintenanceCommitPatterns = Enumerable.Range(0, 30).Select(i => $"pattern {i}").ToList();
        Assert.True(_update.Validate(request).IsValid);

        request.MaintenanceCommitPatterns = Enumerable.Range(0, 31).Select(i => $"pattern {i}").ToList();
        Assert.False(_update.Validate(request).IsValid);
    }

    [Theory]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("  ab  ", false)]
    [InlineData("   abc   ", true)]
    [InlineData("", false)]
    [InlineData("     ", false)]
    public void Update_EachPatternHas3To50Characters_NotCountingTheSpacesAround(string pattern, bool valid)
    {
        var request = Valid();
        request.MaintenanceCommitPatterns = [pattern];

        Assert.Equal(valid, _update.Validate(request).IsValid);
    }

    [Fact]
    public void Update_Pattern50CharactersIsTheLongestAllowed()
    {
        var request = Valid();
        request.MaintenanceCommitPatterns = [new string('a', 50)];
        Assert.True(_update.Validate(request).IsValid);

        request.MaintenanceCommitPatterns = [new string('a', 51)];
        Assert.False(_update.Validate(request).IsValid);
    }

    [Fact]
    public void Update_APatternTwice_IsInvalid_WhateverTheCaseOrTheSpaces()
    {
        var request = Valid();
        request.MaintenanceCommitPatterns = ["chore", " CHORE "];

        Assert.False(_update.Validate(request).IsValid);
    }

    [Fact]
    public void Update_ANullPattern_IsInvalid_NotACrash()
    {
        var request = Valid();
        request.MaintenanceCommitPatterns = ["chore", null!];

        Assert.False(_update.Validate(request).IsValid);
    }
}
