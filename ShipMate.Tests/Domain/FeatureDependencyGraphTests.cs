using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Domain;

public class FeatureDependencyGraphTests
{
    [Fact]
    public void FindCycle_AcyclicGraph_ReturnsEmpty()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        var c = TestData.Feature("c");
        TestData.DependsOn(b, a);
        TestData.DependsOn(c, b);
        TestData.DependsOn(c, a);

        Assert.Empty(FeatureDependencyGraph.FindCycle([a, b, c]));
    }

    [Fact]
    public void FindCycle_ThreeFeatureCycle_ReturnsClosedLoop()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        var c = TestData.Feature("c");
        TestData.DependsOn(a, b);
        TestData.DependsOn(b, c);
        TestData.DependsOn(c, a);

        var cycle = FeatureDependencyGraph.FindCycle([a, b, c]);

        Assert.Equal(4, cycle.Count);
        Assert.Equal(cycle[0], cycle[^1]);
        Assert.Equivalent(new[] { a.Id, b.Id, c.Id }, cycle.Distinct());
    }

    [Fact]
    public void FindCycle_SelfDependency_IsACycle()
    {
        var a = TestData.Feature("a");
        TestData.DependsOn(a, a);

        Assert.NotEmpty(FeatureDependencyGraph.FindCycle([a]));
    }

    [Fact]
    public void FindCycleIfLinked_LinkClosingALoop_ReturnsTheLoop()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        TestData.DependsOn(b, a);

        // a depending on b would close a -> b -> a.
        var cycle = FeatureDependencyGraph.FindCycleIfLinked([a, b], a.Id, b.Id);

        Assert.Equal([a.Id, b.Id, a.Id], cycle);
    }

    [Fact]
    public void FindCycleIfLinked_SafeLink_ReturnsEmpty()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        var c = TestData.Feature("c");
        TestData.DependsOn(b, a);

        Assert.Empty(FeatureDependencyGraph.FindCycleIfLinked([a, b, c], c.Id, b.Id));
    }

    [Fact]
    public void FindCycleIfLinked_IndirectLoop_IsDetected()
    {
        var a = TestData.Feature("a");
        var b = TestData.Feature("b");
        var c = TestData.Feature("c");
        TestData.DependsOn(c, b);
        TestData.DependsOn(b, a);

        Assert.NotEmpty(FeatureDependencyGraph.FindCycleIfLinked([a, b, c], a.Id, c.Id));
    }
}
