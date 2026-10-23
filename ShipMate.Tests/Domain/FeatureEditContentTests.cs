namespace ShipMate.Tests.Domain;

public class FeatureEditContentTests
{
    private static ShipMate.Domain.Entities.Feature Scored()
    {
        var feature = TestData.Feature("a");
        feature.Scope = "old scope";
        feature.ProposedComplexity = 2;
        feature.Complexity = 3;
        return feature;
    }

    [Fact]
    public void NewDescription_DropsTheScore()
    {
        var feature = Scored();

        feature.EditContent(null, "new description", null);

        Assert.Equal("new description", feature.Description);
        Assert.Null(feature.Complexity);
        Assert.Null(feature.ProposedComplexity);
    }

    [Fact]
    public void NewScope_DropsTheScore()
    {
        var feature = Scored();

        feature.EditContent(null, null, "new scope");

        Assert.Null(feature.Complexity);
    }

    [Fact]
    public void RenameOnly_KeepsTheScore()
    {
        var feature = Scored();

        feature.EditContent("renamed", null, null);

        Assert.Equal("renamed", feature.Name);
        Assert.Equal(3, feature.Complexity);
    }

    [Fact]
    public void SameDescriptionAndScopeSentAgain_KeepsTheScore()
    {
        var feature = Scored();

        feature.EditContent(null, feature.Description, "old scope");

        Assert.Equal(3, feature.Complexity);
    }
}
