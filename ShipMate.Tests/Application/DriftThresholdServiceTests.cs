using NSubstitute;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

public class DriftThresholdServiceTests
{
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly InMemoryAuditTrailRepository _auditEntries = new();
    private readonly DriftThresholdService _service;
    private readonly Workspace _workspace = new() { DriftConfidenceThreshold = 0.75m };
    private readonly Guid _manager = Guid.NewGuid();
    private readonly Guid _developer = Guid.NewGuid();

    public DriftThresholdServiceTests()
    {
        _service = new DriftThresholdService(_guard, _workspaces, _auditEntries, new AuditTrail(_auditEntries));

        _guard.GetWorkspaceAsManagerAsync(_manager, _workspace.Id).Returns(_workspace);
        _guard.GetWorkspaceAsManagerAsync(_developer, _workspace.Id)
            .Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());
    }

    private Task<DriftThresholdDto> Change(decimal value, Guid? by = null) =>
        _service.ChangeAsync(by ?? _manager, _workspace.Id, new ChangeDriftThresholdRequest { Value = value });

    private async Task GivenEarlierChange(DateTime at, string type = AuditTypes.ThresholdChanged)
    {
        await new AuditTrail(_auditEntries).AppendAsync(_workspace.Id, type, _manager, new { earlier = true });
        _auditEntries.Entries[^1].CreatedAt = at;
    }

    // ---- a change that goes through ----

    [Fact]
    public async Task RaisingTheThreshold_SetsItAndRecordsTheChange()
    {
        var result = await Change(0.80m);

        Assert.Equal(0.80m, _workspace.DriftConfidenceThreshold);
        Assert.Equal(0.80m, result.Value);
        Assert.Equal(1, result.ChangesUsed);
        Assert.Equal(3, result.MaxChanges);
        Assert.NotNull(result.NextChangeAllowedAt);
        await _workspaces.Received(1).SaveChangesAsync();

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Equal(AuditTypes.ThresholdChanged, record.AuditType);
        Assert.Equal(_manager, record.ActorUserId);
        Assert.Contains("\"from\":0.75", record.Payload);
        Assert.Contains("\"to\":0.80", record.Payload);
    }

    [Fact]
    public async Task LoweringTheThreshold_AlsoRecordsAGovernanceDilution()
    {
        await Change(0.70m);

        Assert.Equal(
            [AuditTypes.ThresholdChanged, AuditTypes.GovernanceDilution],
            _auditEntries.Entries.OrderBy(e => e.Sequence).Select(e => e.AuditType));
        Assert.Null(AuditChain.FindFirstBroken(_auditEntries.Entries.OrderBy(e => e.Sequence)));
    }

    [Fact]
    public async Task RaisingTheThreshold_RecordsNoDilution()
    {
        await Change(0.85m);

        Assert.DoesNotContain(_auditEntries.Entries, e => e.AuditType == AuditTypes.GovernanceDilution);
    }

    [Fact]
    public async Task AChangeAfterTheWait_IsAllowedAndCountsAsTheNext()
    {
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-8));

        var result = await Change(0.80m);

        Assert.Equal(2, result.ChangesUsed);
    }

    [Fact]
    public async Task TheLastChange_LeavesNoNextDate()
    {
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-30));
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-20));

        var result = await Change(0.80m);

        Assert.Equal(3, result.ChangesUsed);
        Assert.Null(result.NextChangeAllowedAt);
    }

    [Fact]
    public async Task OnlyThresholdChangesAreCounted()
    {
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-1), AuditTypes.ScopeBypass);
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-1), AuditTypes.GovernanceDilution);

        var result = await Change(0.80m);

        Assert.Equal(1, result.ChangesUsed);
    }

    // ---- refused ----

    private async Task AssertRefusedAndNothingChanged<T>(decimal value, int recordsBefore = 0) where T : Exception
    {
        var before = _workspace.DriftConfidenceThreshold;

        await Assert.ThrowsAsync<T>(() => Change(value));

        Assert.Equal(before, _workspace.DriftConfidenceThreshold);
        Assert.Equal(recordsBefore, _auditEntries.Entries.Count);
        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(0.50)]
    [InlineData(0.69)]
    [InlineData(0.86)]
    public Task ValueOutOfRange_IsRefused(double value) =>
        AssertRefusedAndNothingChanged<ThresholdOutOfRangeException>((decimal)value);

    [Fact]
    public Task TheCurrentValue_IsRefused() =>
        AssertRefusedAndNothingChanged<ThresholdUnchangedException>(0.75m);

    [Fact]
    public async Task AChangeWithinSevenDays_IsRefusedWithTheDateItBecomesPossible()
    {
        var earlier = DateTime.UtcNow.AddDays(-2);
        await GivenEarlierChange(earlier);

        var error = await Assert.ThrowsAsync<ThresholdChangeCooldownException>(() => Change(0.80m));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(earlier.AddDays(7), error.Details!.GetType().GetProperty("NextChangeAllowedAt")!.GetValue(error.Details));
        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task AFourthChange_IsRefused()
    {
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-60));
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-40));
        await GivenEarlierChange(DateTime.UtcNow.AddDays(-20));

        await AssertRefusedAndNothingChanged<ThresholdChangeLimitReachedException>(0.80m, recordsBefore: 3);
    }

    [Fact]
    public async Task ANonManager_IsRefused()
    {
        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => Change(0.80m, by: _developer));

        Assert.Equal(0.75m, _workspace.DriftConfidenceThreshold);
        Assert.Empty(_auditEntries.Entries);
    }
}
