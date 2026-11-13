using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ShipMate.Application.DTOs.Verify;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class VerificationServiceTests
{
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IVerificationChecklistRepository _checklists = Substitute.For<IVerificationChecklistRepository>();
    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly InMemoryAuditTrailRepository _auditEntries = new();
    private readonly VerificationService _service;

    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _manager = Guid.NewGuid();
    private readonly Guid _developer = Guid.NewGuid();
    private readonly SnapshotFeature _a = Feature("a");
    private readonly SnapshotFeature _b = Feature("b");
    private readonly SnapshotFeature _replaced = Feature("replaced", SnapshotFeatureStatus.Replaced);
    private readonly SnapshotFeature _backup = Feature("backup", SnapshotFeatureStatus.Backup);
    private readonly MvpSnapshot _snapshot;
    private readonly VerificationChecklist _checklist;
    private readonly List<DriftAlert> _openAlerts = new();

    public VerificationServiceTests()
    {
        _service = new VerificationService(_guard, _checklists, _submissions, _alerts, new AuditTrail(_auditEntries));

        _snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 2 };
        foreach (var feature in new[] { _a, _b, _replaced, _backup })
        {
            _snapshot.Features.Add(feature);
        }

        _checklist = VerificationChecklist.CreateFor(_snapshot);
        _checklist.MvpSnapshot = _snapshot;

        _checklists.GetByVersionAsync(_workspaceId, 2).Returns(_checklist);
        _alerts.GetOpenByWorkspaceAsync(_workspaceId).Returns(_openAlerts);
        _guard.GetWorkspaceAsManagerAsync(_developer, _workspaceId).ThrowsAsync(new WorkspacePermissionDeniedException());
    }

    private static SnapshotFeature Feature(string name, SnapshotFeatureStatus status = SnapshotFeatureStatus.Locked) =>
        new() { FeatureId = Guid.NewGuid(), Name = name, Complexity = 2, Status = status };

    private void NewestSubmissionOf(SnapshotFeature feature, BuildSubmissionStatus status) =>
        _submissions.GetLatestAsync(_workspaceId, feature.FeatureId)
            .Returns(new BuildSubmission { WorkspaceId = _workspaceId, FeatureId = feature.FeatureId, Status = status });

    private void OpenDriftOn(SnapshotFeature feature, int raisedInVersion = 2) =>
        _openAlerts.Add(new DriftAlert { WorkspaceId = _workspaceId, FeatureId = feature.FeatureId, SnapshotVersionAtCheck = raisedInVersion });

    private async Task TickEverythingExcept(string leftOut)
    {
        foreach (var key in VerificationItemKeys.All.Where(k => k != leftOut))
        {
            await _service.CheckItemAsync(_manager, _workspaceId, 2, key, new CheckVerificationItemRequest { Checked = true });
        }
    }

    private List<AuditTrailEntry> Records(string type) => _auditEntries.Entries.Where(e => e.AuditType == type).ToList();

    // ---- reading ----

    [Fact]
    public async Task GetAll_ListsNewestFirst_AndFlagsTheLatest()
    {
        var older = VerificationChecklist.CreateFor(new MvpSnapshot { WorkspaceId = _workspaceId, Version = 1 });
        older.Check(VerificationItemKeys.MainUserJourney, true, null, _manager, DateTime.UtcNow);
        _checklists.GetByWorkspaceIdAsync(_workspaceId).Returns(new List<VerificationChecklist> { _checklist, older });

        var result = await _service.GetAllAsync(_manager, _workspaceId);

        Assert.Equal([2, 1], result.Select(c => c.Version));
        Assert.True(result[0].IsLatest);
        Assert.False(result[1].IsLatest);
        Assert.Equal(5, result[0].MissingCount);
        Assert.Equal(4, result[1].MissingCount);
    }

    [Fact]
    public async Task Get_UnknownVersion_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(_manager, _workspaceId, 9));
    }

    [Fact]
    public async Task Get_ShowsEveryItemInTheFixedOrder_AndWhatIsMissing()
    {
        var result = await _service.GetAsync(_manager, _workspaceId, 2);

        Assert.Equal(2, result.Version);
        Assert.False(result.AllItemsChecked);
        Assert.Equal(VerificationItemKeys.All, result.Items.Select(i => i.Key));
        Assert.Equal(VerificationItemKeys.All, result.MissingItems);
    }

    [Fact]
    public async Task Get_ShowsOpenDriftAndFeatureProgress_WithoutBlocking()
    {
        NewestSubmissionOf(_a, BuildSubmissionStatus.Approved);
        NewestSubmissionOf(_b, BuildSubmissionStatus.Drift);
        OpenDriftOn(_b);

        var result = await _service.GetAsync(_manager, _workspaceId, 2);

        Assert.Equal(1, result.Info.OpenDriftCount);
        Assert.Equal(1, result.Info.ApprovedFeatures);
        Assert.Equal(2, result.Info.LockedFeatures);
        Assert.False(result.AllItemsChecked);
    }

    [Fact]
    public async Task Get_CountsADriftRaisedInAnEarlierVersion_WhenItsFeatureIsStillLocked()
    {
        OpenDriftOn(_a, raisedInVersion: 1);

        var result = await _service.GetAsync(_manager, _workspaceId, 2);

        Assert.Equal(1, result.Info.OpenDriftCount);
    }

    [Fact]
    public async Task Get_DoesNotCountADriftOnAReplacedOrBackupFeature()
    {
        OpenDriftOn(_replaced, raisedInVersion: 1);
        OpenDriftOn(_backup);

        var result = await _service.GetAsync(_manager, _workspaceId, 2);

        Assert.Equal(0, result.Info.OpenDriftCount);
    }

    // ---- ticking ----

    [Fact]
    public async Task CheckItem_ByAMember_RecordsWhoAndWhen()
    {
        var result = await _service.CheckItemAsync(_developer, _workspaceId, 2, VerificationItemKeys.BasicCrud,
            new CheckVerificationItemRequest { Checked = true, Note = "Checked on the live site" });

        var item = result.Items.Single(i => i.Key == VerificationItemKeys.BasicCrud);
        Assert.True(item.Checked);
        Assert.Equal(_developer, item.CheckedByUserId);
        Assert.NotNull(item.CheckedAt);
        Assert.Equal("Checked on the live site", item.Note);
        await _checklists.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task CheckItem_UnknownKey_IsBadRequest()
    {
        var ex = await Assert.ThrowsAsync<VerificationItemUnknownException>(() => _service.CheckItemAsync(
            _developer, _workspaceId, 2, "deploys_on_friday", new CheckVerificationItemRequest { Checked = true }));

        Assert.Equal(400, ex.StatusCode);
        await _checklists.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task CheckItem_UnknownVersion_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CheckItemAsync(
            _developer, _workspaceId, 9, VerificationItemKeys.BasicCrud, new CheckVerificationItemRequest { Checked = true }));
    }

    [Fact]
    public async Task CheckItem_ANotApplicableItem_IsConflict()
    {
        await _service.SetApplicabilityAsync(_manager, _workspaceId, 2, VerificationItemKeys.Authentication,
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = "The product has no accounts" });

        await Assert.ThrowsAsync<VerificationItemNotApplicableException>(() => _service.CheckItemAsync(
            _developer, _workspaceId, 2, VerificationItemKeys.Authentication, new CheckVerificationItemRequest { Checked = true }));
    }

    [Fact]
    public async Task CheckItem_CompletingTheList_AppendsVerifyCompleted()
    {
        await TickEverythingExcept(VerificationItemKeys.BasicCrud);
        Assert.Empty(Records(AuditTypes.VerifyCompleted));

        var result = await _service.CheckItemAsync(_developer, _workspaceId, 2, VerificationItemKeys.BasicCrud,
            new CheckVerificationItemRequest { Checked = true });

        Assert.True(result.AllItemsChecked);
        Assert.NotNull(result.CompletedAt);
        var record = Assert.Single(Records(AuditTypes.VerifyCompleted));
        Assert.Equal(_developer, record.ActorUserId);
        Assert.Contains("\"version\":2", record.Payload);
    }

    [Fact]
    public async Task CheckItem_NotCompleting_AppendsNothing()
    {
        await _service.CheckItemAsync(_developer, _workspaceId, 2, VerificationItemKeys.BasicCrud,
            new CheckVerificationItemRequest { Checked = true });

        Assert.Empty(_auditEntries.Entries);
    }

    [Fact]
    public async Task CheckItem_UntickingAfterCompletion_ReopensWithoutANewRecord()
    {
        await TickEverythingExcept("none");
        Assert.Single(Records(AuditTypes.VerifyCompleted));

        var result = await _service.CheckItemAsync(_developer, _workspaceId, 2, VerificationItemKeys.BasicCrud,
            new CheckVerificationItemRequest { Checked = false });

        Assert.False(result.AllItemsChecked);
        Assert.Null(result.CompletedAt);
        Assert.Single(Records(AuditTypes.VerifyCompleted));
    }

    // ---- skipping a check ----

    [Fact]
    public async Task SetApplicability_ByADeveloper_IsDenied()
    {
        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.SetApplicabilityAsync(
            _developer, _workspaceId, 2, VerificationItemKeys.Authentication,
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = "The product has no accounts" }));

        Assert.True(_checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.Authentication).Applicable);
    }

    [Fact]
    public async Task SetApplicability_ByTheManager_AppendsItsRecordWithTheReason()
    {
        var result = await _service.SetApplicabilityAsync(_manager, _workspaceId, 2, VerificationItemKeys.Authentication,
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = "  The product has no accounts  " });

        var item = result.Items.Single(i => i.Key == VerificationItemKeys.Authentication);
        Assert.False(item.Applicable);
        Assert.Equal("The product has no accounts", item.NotApplicableReason);

        var record = Assert.Single(Records(AuditTypes.VerifyItemNotApplicable));
        Assert.Equal(_manager, record.ActorUserId);
        Assert.Contains("authentication", record.Payload);
        Assert.Contains("The product has no accounts", record.Payload);
    }

    [Fact]
    public async Task SetApplicability_SayingAgainWhatIsAlreadySo_AppendsNothing()
    {
        await _service.SetApplicabilityAsync(_manager, _workspaceId, 2, VerificationItemKeys.Authentication,
            new SetVerificationItemApplicabilityRequest { Applicable = true });

        Assert.Empty(_auditEntries.Entries);
    }

    [Fact]
    public async Task SetApplicability_ThatCompletesTheList_AppendsBothRecords()
    {
        await TickEverythingExcept(VerificationItemKeys.Authentication);

        var result = await _service.SetApplicabilityAsync(_manager, _workspaceId, 2, VerificationItemKeys.Authentication,
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = "The product has no accounts" });

        Assert.True(result.AllItemsChecked);
        Assert.Single(Records(AuditTypes.VerifyItemNotApplicable));
        Assert.Single(Records(AuditTypes.VerifyCompleted));
    }

    [Fact]
    public async Task SetApplicability_MainUserJourney_IsConflict()
    {
        var ex = await Assert.ThrowsAsync<VerificationMainJourneyRequiredException>(() => _service.SetApplicabilityAsync(
            _manager, _workspaceId, 2, VerificationItemKeys.MainUserJourney,
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = "Not for this product" }));

        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(_auditEntries.Entries);
    }

    [Fact]
    public async Task SetApplicability_UnknownKey_IsBadRequest()
    {
        await Assert.ThrowsAsync<VerificationItemUnknownException>(() => _service.SetApplicabilityAsync(
            _manager, _workspaceId, 2, "nope", new SetVerificationItemApplicabilityRequest { Applicable = true }));
    }

    // ---- what SHIP and NBA read ----

    [Fact]
    public async Task GetReadiness_NoChecklist_IsMissing()
    {
        var readiness = await _service.GetReadinessAsync(_workspaceId, 9);

        Assert.False(readiness.Exists);
        Assert.False(readiness.AllItemsChecked);
        Assert.Null(readiness.ChecklistId);
    }

    [Fact]
    public async Task GetReadiness_ItemsMissing_ListsThem()
    {
        await _service.CheckItemAsync(_manager, _workspaceId, 2, VerificationItemKeys.MainUserJourney,
            new CheckVerificationItemRequest { Checked = true });

        var readiness = await _service.GetReadinessAsync(_workspaceId, 2);

        Assert.True(readiness.Exists);
        Assert.False(readiness.AllItemsChecked);
        Assert.Equal(
            [VerificationItemKeys.Authentication, VerificationItemKeys.ProductionConfig, VerificationItemKeys.BasicCrud, VerificationItemKeys.ErrorHandling],
            readiness.MissingItems);
        Assert.Equal(_checklist.Id, readiness.ChecklistId);
    }

    [Fact]
    public async Task GetReadiness_Complete_HasNoMissingItems_AndTheTimeItWasDone()
    {
        await TickEverythingExcept("none");

        var readiness = await _service.GetReadinessAsync(_workspaceId, 2);

        Assert.True(readiness.AllItemsChecked);
        Assert.Empty(readiness.MissingItems);
        Assert.NotNull(readiness.CompletedAt);
    }

    [Fact]
    public async Task GetReadiness_ChecksNobodysAccess()
    {
        await _service.GetReadinessAsync(_workspaceId, 2);

        await _guard.DidNotReceiveWithAnyArgs().GetWorkspaceAsMemberAsync(default, default);
        await _guard.DidNotReceiveWithAnyArgs().GetWorkspaceAsManagerAsync(default, default);
    }
}
