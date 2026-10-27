using NSubstitute;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

public class DriftAlertServiceTests
{
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly InMemoryAuditTrailRepository _auditEntries = new();
    private readonly DriftAlertService _service;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _manager = Guid.NewGuid();
    private readonly Guid _developer = Guid.NewGuid();
    private readonly DriftAlert _alert;

    public DriftAlertServiceTests()
    {
        _service = new DriftAlertService(_guard, _alerts, _assignments, _members, new AuditTrail(_auditEntries), _notifier);

        _alert = new DriftAlert
        {
            WorkspaceId = _workspaceId, FeatureId = Guid.NewGuid(), SubmissionId = Guid.NewGuid(),
            SnapshotVersionAtCheck = 2, ThresholdAtCheck = 0.75m, ConfidenceScore = 0.4m
        };
        _alerts.GetByIdAsync(_workspaceId, _alert.Id).Returns(_alert);

        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(new List<FeatureAssignment>
        {
            new() { FeatureId = _alert.FeatureId, AssignedUserId = _developer }
        });
        GivenActiveMembers(2);

        _guard.GetWorkspaceAsManagerAsync(_developer, _workspaceId)
            .Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());
    }

    private void GivenActiveMembers(int count) =>
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns(
            Enumerable.Range(0, count).Select(_ => new WorkspaceMember { Status = WorkspaceMemberStatus.Active }).ToList());

    private static OverrideDriftAlertRequest Reason(string text) => new() { Reason = text };

    // ---- listing ----

    [Fact]
    public async Task GetAll_PassesTheStatusFilterAndMapsTheAlerts()
    {
        _alerts.GetByWorkspaceIdAsync(_workspaceId, DriftAlertStatus.Unresolved).Returns(new List<DriftAlert> { _alert });

        var result = await _service.GetAllAsync(_developer, _workspaceId, DriftAlertStatus.Unresolved);

        var dto = Assert.Single(result);
        Assert.Equal(_alert.Id, dto.Id);
        Assert.Equal(0.4m, dto.ConfidenceScore);
        Assert.Equal(DriftAlertStatus.Unresolved, dto.Status);
    }

    // ---- acknowledge ----

    [Fact]
    public async Task Acknowledge_ByTheDeveloperHoldingTheFeature_MovesToAcknowledged()
    {
        var dto = await _service.AcknowledgeAsync(_developer, _workspaceId, _alert.Id);

        Assert.Equal(DriftAlertStatus.Acknowledged, dto.Status);
        Assert.Equal(_developer, _alert.AcknowledgedByUserId);
        await _alerts.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Acknowledge_BySomeoneWhoDoesNotHoldTheFeature_IsRefused()
    {
        await Assert.ThrowsAsync<NotFeatureHolderException>(
            () => _service.AcknowledgeAsync(_manager, _workspaceId, _alert.Id));

        Assert.Equal(DriftAlertStatus.Unresolved, _alert.Status);
    }

    [Fact]
    public async Task Acknowledge_WhenTheHolderReleasedTheFeature_IsRefused()
    {
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(new List<FeatureAssignment>
        {
            new() { FeatureId = _alert.FeatureId, AssignedUserId = _developer, ReleasedAt = DateTime.UtcNow }
        });

        await Assert.ThrowsAsync<NotFeatureHolderException>(
            () => _service.AcknowledgeAsync(_developer, _workspaceId, _alert.Id));
    }

    [Theory]
    [InlineData(DriftAlertStatus.Acknowledged)]
    [InlineData(DriftAlertStatus.Overridden)]
    [InlineData(DriftAlertStatus.Resolved)]
    public async Task Acknowledge_WhenNotUnresolved_IsRefused(DriftAlertStatus status)
    {
        _alert.Status = status;

        await Assert.ThrowsAsync<DriftAlertNotOpenException>(
            () => _service.AcknowledgeAsync(_developer, _workspaceId, _alert.Id));

        Assert.Equal(status, _alert.Status);
    }

    [Fact]
    public async Task Acknowledge_UnknownAlert_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AcknowledgeAsync(_developer, _workspaceId, Guid.NewGuid()));
    }

    // ---- real-time messages ----

    [Fact]
    public async Task Acknowledge_IsAnnouncedToTheWorkspace()
    {
        await _service.AcknowledgeAsync(_developer, _workspaceId, _alert.Id);

        await _notifier.Received(1).PublishAsync(_workspaceId, WorkspaceEvents.DriftAlertAcknowledged, Arg.Any<object>());
    }

    [Fact]
    public async Task Override_IsAnnouncedToTheWorkspace()
    {
        await _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("agreed"));

        await _notifier.Received(1).PublishAsync(_workspaceId, WorkspaceEvents.ManagerOverrideExecuted, Arg.Any<object>());
    }

    [Fact]
    public async Task ARefusedOverrideOrAcknowledge_AnnouncesNothing()
    {
        _alert.Status = DriftAlertStatus.Resolved;

        await Assert.ThrowsAsync<DriftAlertNotOpenException>(() => _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("late")));
        await Assert.ThrowsAsync<DriftAlertNotOpenException>(() => _service.AcknowledgeAsync(_developer, _workspaceId, _alert.Id));

        await _notifier.DidNotReceiveWithAnyArgs().PublishAsync(default, default!, default!);
    }

    // ---- override ----

    [Theory]
    [InlineData(DriftAlertStatus.Unresolved)]
    [InlineData(DriftAlertStatus.Acknowledged)]
    public async Task Override_ByAManager_MovesToOverriddenWithTheReason(DriftAlertStatus from)
    {
        _alert.Status = from;

        var dto = await _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("  Client asked for it  "));

        Assert.Equal(DriftAlertStatus.Overridden, dto.Status);
        Assert.Equal("Client asked for it", dto.OverrideReason);
        Assert.Equal(_manager, _alert.OverriddenByUserId);
        await _alerts.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Override_WritesAScopeBypassRecordNamingTheAlertTheReasonAndTheManager()
    {
        await _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("Client asked for it"));

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Equal(AuditTypes.ScopeBypass, record.AuditType);
        Assert.Equal(_manager, record.ActorUserId);
        Assert.Equal(_workspaceId, record.WorkspaceId);
        Assert.Contains(_alert.Id.ToString(), record.Payload);
        Assert.Contains("Client asked for it", record.Payload);
    }

    [Fact]
    public async Task Override_InAWorkspaceWithOneMember_AlsoWritesTheSoloNote()
    {
        GivenActiveMembers(1);

        await _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("my call"));

        Assert.Equal(
            [AuditTypes.ScopeBypass, AuditTypes.SoloOverrideLogged],
            _auditEntries.Entries.OrderBy(e => e.Sequence).Select(e => e.AuditType));
        Assert.Null(AuditChain.FindFirstBroken(_auditEntries.Entries.OrderBy(e => e.Sequence)));
    }

    [Fact]
    public async Task Override_InAWorkspaceWithSeveralMembers_WritesNoSoloNote()
    {
        await _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("agreed"));

        Assert.DoesNotContain(_auditEntries.Entries, e => e.AuditType == AuditTypes.SoloOverrideLogged);
    }

    [Fact]
    public async Task Override_ByANonManager_IsRefusedAndWritesNothing()
    {
        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(
            () => _service.OverrideAsync(_developer, _workspaceId, _alert.Id, Reason("trust me")));

        Assert.Equal(DriftAlertStatus.Unresolved, _alert.Status);
        Assert.Empty(_auditEntries.Entries);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Overridden)]
    [InlineData(DriftAlertStatus.Resolved)]
    public async Task Override_WhenAlreadyClosed_IsRefusedAndWritesNothing(DriftAlertStatus status)
    {
        _alert.Status = status;

        await Assert.ThrowsAsync<DriftAlertNotOpenException>(
            () => _service.OverrideAsync(_manager, _workspaceId, _alert.Id, Reason("again")));

        Assert.Empty(_auditEntries.Entries);
    }
}
