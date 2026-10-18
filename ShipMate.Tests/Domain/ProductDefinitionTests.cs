using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Tests.Domain;

public class ProductDefinitionTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MarkModified_WhileReadyForLock_ReturnsToDraft()
    {
        var definition = new ProductDefinition { Status = ProductDefinitionStatus.ReadyForLock };

        definition.MarkModified(Now);

        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
        Assert.Equal(Now, definition.UpdatedAt);
    }

    [Fact]
    public void MarkModified_WhileDraft_StaysDraft()
    {
        var definition = new ProductDefinition();

        definition.MarkModified(Now);

        Assert.Equal(ProductDefinitionStatus.Draft, definition.Status);
    }

    [Fact]
    public void NextFeaturePosition_StartsAtZeroThenFollowsHighest()
    {
        var definition = new ProductDefinition();
        Assert.Equal(0, definition.NextFeaturePosition());

        definition.Features.Add(TestData.Feature(position: 4));
        definition.Features.Add(TestData.Feature(position: 2));

        Assert.Equal(5, definition.NextFeaturePosition());
    }

    [Theory]
    [InlineData(null, null, null, true)]
    [InlineData("", "  ", null, true)]
    [InlineData("rule", null, null, false)]
    [InlineData(null, null, "rule", false)]
    public void ProjectConstraints_IsEmpty(string? customer, string? company, string? team, bool expected)
    {
        var constraints = new ProjectConstraints { Customer = customer, Company = company, Team = team };

        Assert.Equal(expected, constraints.IsEmpty);
    }
}
