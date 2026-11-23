using Microsoft.Extensions.Configuration;
using NSubstitute;
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

    // ---------- Trying patterns ----------

    private static TestMaintenancePatternsRequestValidator Test(string? maxDiff = null)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration["Build:MaxDiffCharacters"].Returns(maxDiff);
        return new TestMaintenancePatternsRequestValidator(configuration);
    }

    [Fact]
    public void Test_AMessageAlone_IsValid() =>
        Assert.True(Test().Validate(new TestMaintenancePatternsRequest { CommitMessages = ["fix typo"] }).IsValid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Test_BetweenOneAndAHundredMessages(int count, bool valid)
    {
        var request = new TestMaintenancePatternsRequest { CommitMessages = Enumerable.Repeat("m", count).ToList() };

        Assert.Equal(valid, Test().Validate(request).IsValid);
    }

    [Fact]
    public void Test_ABlankOrOverlongMessage_IsInvalid()
    {
        Assert.False(Test().Validate(new TestMaintenancePatternsRequest { CommitMessages = ["  "] }).IsValid);
        Assert.False(Test().Validate(new TestMaintenancePatternsRequest { CommitMessages = [new string('a', 1001)] }).IsValid);
    }

    [Fact]
    public void Test_PatternsWhenGiven_FollowTheSameRulesAsSaving()
    {
        var request = new TestMaintenancePatternsRequest { CommitMessages = ["m"], Patterns = ["ab"] };
        Assert.False(Test().Validate(request).IsValid);

        request.Patterns = ["abc"];
        Assert.True(Test().Validate(request).IsValid);

        request.Patterns = null;
        Assert.True(Test().Validate(request).IsValid);
    }

    [Fact]
    public void Test_AtMost100Files_EachWithAPath()
    {
        File(out var one);
        var request = new TestMaintenancePatternsRequest
        {
            CommitMessages = ["m"], Files = Enumerable.Range(0, 100).Select(_ => one).ToList()
        };
        Assert.True(Test().Validate(request).IsValid);

        request.Files = Enumerable.Range(0, 101).Select(_ => one).ToList();
        Assert.False(Test().Validate(request).IsValid);

        request.Files = [new TestChangedFileDto { Path = " " }];
        Assert.False(Test().Validate(request).IsValid);
    }

    [Fact]
    public void Test_PatchesTogetherStayWithinTheBuildDiffLimit()
    {
        var request = new TestMaintenancePatternsRequest
        {
            CommitMessages = ["m"],
            Files =
            [
                new TestChangedFileDto { Path = "a", Patch = new string('x', 30_000) },
                new TestChangedFileDto { Path = "b", Patch = new string('x', 30_000) }
            ]
        };
        Assert.True(Test().Validate(request).IsValid);

        request.Files[1].Patch = new string('x', 30_001);
        Assert.False(Test().Validate(request).IsValid);
    }

    [Fact]
    public void Test_TheLimitFollowsTheBuildConfiguration()
    {
        var request = new TestMaintenancePatternsRequest
        {
            CommitMessages = ["m"], Files = [new TestChangedFileDto { Path = "a", Patch = new string('x', 21) }]
        };

        Assert.False(Test("20").Validate(request).IsValid);
        Assert.True(Test("21").Validate(request).IsValid);
        Assert.True(Test("not a number").Validate(request).IsValid);
    }

    private static void File(out TestChangedFileDto file) => file = new TestChangedFileDto { Path = "src/a.ts" };
}
