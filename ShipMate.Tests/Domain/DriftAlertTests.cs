using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Domain;

public class DriftAlertTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _submission = Guid.NewGuid();

    private static DriftAlert In(DriftAlertStatus status) => new() { Status = status };

    [Fact]
    public void ANewAlert_IsUnresolvedAndOpen()
    {
        var alert = new DriftAlert();

        Assert.Equal(DriftAlertStatus.Unresolved, alert.Status);
        Assert.True(alert.IsOpen);
    }

    [Fact]
    public void Acknowledge_FromUnresolved_RecordsWhoAndWhen()
    {
        var alert = In(DriftAlertStatus.Unresolved);

        Assert.True(alert.Acknowledge(_user, Now));

        Assert.Equal(DriftAlertStatus.Acknowledged, alert.Status);
        Assert.Equal(_user, alert.AcknowledgedByUserId);
        Assert.Equal(Now, alert.AcknowledgedAt);
        Assert.True(alert.IsOpen);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Acknowledged)]
    [InlineData(DriftAlertStatus.Overridden)]
    [InlineData(DriftAlertStatus.Resolved)]
    public void Acknowledge_FromAnyOtherState_ChangesNothing(DriftAlertStatus from)
    {
        var alert = In(from);

        Assert.False(alert.Acknowledge(_user, Now));

        Assert.Equal(from, alert.Status);
        Assert.Null(alert.AcknowledgedAt);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Unresolved)]
    [InlineData(DriftAlertStatus.Acknowledged)]
    public void Override_FromAnOpenState_RecordsWhoWhenAndWhy(DriftAlertStatus from)
    {
        var alert = In(from);

        Assert.True(alert.Override(_user, "Client asked for it", Now));

        Assert.Equal(DriftAlertStatus.Overridden, alert.Status);
        Assert.Equal(_user, alert.OverriddenByUserId);
        Assert.Equal(Now, alert.OverriddenAt);
        Assert.Equal("Client asked for it", alert.OverrideReason);
        Assert.False(alert.IsOpen);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Overridden)]
    [InlineData(DriftAlertStatus.Resolved)]
    public void Override_FromAClosedState_ChangesNothing(DriftAlertStatus from)
    {
        var alert = In(from);

        Assert.False(alert.Override(_user, "late", Now));

        Assert.Equal(from, alert.Status);
        Assert.Null(alert.OverrideReason);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Unresolved)]
    [InlineData(DriftAlertStatus.Acknowledged)]
    public void Resolve_FromAnOpenState_RecordsTheSubmissionThatClosedIt(DriftAlertStatus from)
    {
        var alert = In(from);

        Assert.True(alert.Resolve(_submission, Now));

        Assert.Equal(DriftAlertStatus.Resolved, alert.Status);
        Assert.Equal(_submission, alert.ResolvedBySubmissionId);
        Assert.Equal(Now, alert.ResolvedAt);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Overridden)]
    [InlineData(DriftAlertStatus.Resolved)]
    public void Resolve_FromAClosedState_ChangesNothing(DriftAlertStatus from)
    {
        var alert = In(from);

        Assert.False(alert.Resolve(_submission, Now));

        Assert.Equal(from, alert.Status);
        Assert.Null(alert.ResolvedBySubmissionId);
    }
}
