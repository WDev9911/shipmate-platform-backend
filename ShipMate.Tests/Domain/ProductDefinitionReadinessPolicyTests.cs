using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class ProductDefinitionReadinessPolicyTests
{
    [Fact]
    public void Evaluate_AllResolved_IsReady()
    {
        var definition = TestData.Definition(
            TestData.Feature("a"),
            TestData.Feature("b", status: FeatureStatus.Excluded, verdict: AiVerdict.NotRequired));

        Assert.True(ProductDefinitionReadinessPolicy.Evaluate(definition).IsReady);
    }

    [Fact]
    public void Evaluate_PendingFeature_Blocks()
    {
        var pending = TestData.Feature(origin: FeatureOrigin.FromCustomerMentioned, status: FeatureStatus.PendingConfirmation);

        var readiness = ProductDefinitionReadinessPolicy.Evaluate(TestData.Definition(pending));

        Assert.False(readiness.IsReady);
        Assert.Same(pending, Assert.Single(readiness.PendingFeatures));
    }

    [Fact]
    public void Evaluate_UnresolvedDuplicateAndConflictFlags_Block()
    {
        var duplicate = TestData.Feature("dup");
        duplicate.PossibleDuplicate = true;
        var conflict = TestData.Feature("conflict");
        conflict.PersonaConflict = true;

        var readiness = ProductDefinitionReadinessPolicy.Evaluate(TestData.Definition(duplicate, conflict));

        Assert.False(readiness.IsReady);
        Assert.Single(readiness.UnresolvedDuplicates);
        Assert.Single(readiness.UnresolvedPersonaConflicts);
    }

    [Fact]
    public void Evaluate_FlaggedButExcludedFeature_DoesNotBlock()
    {
        var flagged = TestData.Feature(status: FeatureStatus.Excluded);
        flagged.PossibleDuplicate = true;
        flagged.PersonaConflict = true;

        Assert.True(ProductDefinitionReadinessPolicy.Evaluate(TestData.Definition(flagged)).IsReady);
    }

    [Fact]
    public void Evaluate_DependencyCycle_Blocks()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        TestData.DependsOn(a, b);
        TestData.DependsOn(b, a);

        var readiness = ProductDefinitionReadinessPolicy.Evaluate(TestData.Definition(a, b));

        Assert.False(readiness.IsReady);
        Assert.NotEmpty(readiness.DependencyCycle);
    }
}
