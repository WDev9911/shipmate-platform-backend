using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class LockGatePolicyTests
{
    private static Feature Scored(string name, int complexity, FeatureOrigin origin = FeatureOrigin.FromAi)
    {
        var feature = TestData.Feature(name, origin);
        feature.Complexity = complexity;
        return feature;
    }

    private static LockGateResult Evaluate(IReadOnlyList<Feature> features, params int?[] skills) =>
        LockGatePolicy.Evaluate(features, skills);

    // ---- passing ----

    [Fact]
    public void SoloDev_WithinSkillAndBudget_Passes()
    {
        var result = Evaluate([Scored("a", 3), Scored("b", 4), Scored("c", 2)], 4);

        Assert.True(result.IsPassed);
        Assert.Equal(9, result.TotalComplexity);
        Assert.Equal(10, result.Budget);
    }

    [Fact]
    public void BudgetIsExactlyTheLimit_Passes()
    {
        var result = Evaluate([Scored("a", 5), Scored("b", 5)], 5);

        Assert.True(result.IsPassed);
    }

    [Fact]
    public void Team_BudgetGrowsWithActiveDevelopers()
    {
        var features = new[] { Scored("a", 5), Scored("b", 5), Scored("c", 5), Scored("d", 5) };

        Assert.False(Evaluate(features, 5).IsPassed);
        Assert.True(Evaluate(features, 5, 3).IsPassed);
        Assert.Equal(20, Evaluate(features, 5, 3).Budget);
    }

    // ---- blocking ----

    [Fact]
    public void NoIncludedFeatures_Blocks()
    {
        Assert.True(Evaluate([], 3).NoIncludedFeatures);
        Assert.False(Evaluate([], 3).IsPassed);
    }

    [Fact]
    public void BudgetOneOverTheLimit_Blocks()
    {
        var result = Evaluate([Scored("a", 5), Scored("b", 5), Scored("c", 1)], 5);

        Assert.True(result.BudgetExceeded);
        Assert.False(result.IsPassed);
    }

    [Fact]
    public void FeatureHarderThanTheBestDeveloper_BlocksOnSkillCeiling()
    {
        var hard = Scored("hard", 5);

        var result = Evaluate([hard, Scored("easy", 1)], 3, 4);

        Assert.Equal([hard.Id], result.SkillCeilingViolations.Select(f => f.Id));
        Assert.False(result.IsPassed);
    }

    [Fact]
    public void CeilingIsTheBestDeveloper_NotTheAverage()
    {
        Assert.True(Evaluate([Scored("a", 5)], 1, 5).IsPassed);
    }

    [Fact]
    public void UnscoredFeature_Blocks()
    {
        var unscored = TestData.Feature("unscored");

        var result = Evaluate([Scored("a", 2), unscored], 5);

        Assert.Equal([unscored.Id], result.UnscoredFeatures.Select(f => f.Id));
        Assert.False(result.IsPassed);
    }

    [Fact]
    public void MemberWithoutSkill_BlocksAndStillCountsInTheBudget()
    {
        var result = Evaluate([Scored("a", 2)], 4, null);

        Assert.Equal(1, result.MembersWithoutSkill);
        Assert.Equal(20, result.Budget);
        Assert.False(result.IsPassed);
    }

    [Fact]
    public void NobodyDeclaredASkill_BlocksWithoutFlaggingEveryFeature()
    {
        var result = Evaluate([Scored("a", 2)], new int?[] { null });

        Assert.Empty(result.SkillCeilingViolations);
        Assert.False(result.IsPassed);
    }

    // ---- dependency integrity ----

    [Fact]
    public void DependencyInsideTheSnapshot_Passes()
    {
        var a = Scored("a", 2);
        var b = Scored("b", 2);
        TestData.DependsOn(b, a);

        Assert.True(Evaluate([a, b], 5).IsPassed);
    }

    [Fact]
    public void DependencyOnAFeatureOutsideTheSnapshot_Blocks()
    {
        var b = Scored("b", 2);
        var cut = TestData.Feature("cut", status: FeatureStatus.Excluded);
        TestData.DependsOn(b, cut);

        var result = Evaluate([b], 5);

        var broken = Assert.Single(result.BrokenDependencies);
        Assert.Equal(b.Id, broken.Feature.Id);
        Assert.Equal(cut.Id, broken.MissingFeatureId);
        Assert.False(result.IsPassed);
    }

    [Fact]
    public void AllThreeChecksAreIndependent_EachViolationIsReported()
    {
        var hard = Scored("hard", 5);
        var cut = TestData.Feature("cut", status: FeatureStatus.Excluded);
        TestData.DependsOn(hard, cut);

        var result = Evaluate([hard, Scored("b", 5), Scored("c", 2)], 3);

        Assert.NotEmpty(result.SkillCeilingViolations);
        Assert.True(result.BudgetExceeded);
        Assert.NotEmpty(result.BrokenDependencies);
    }
}
