using System.Text.Json;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ShipMate.Application.DTOs.Ship;
using ShipMate.Application.DTOs.Verify;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Application.Validators.Ship;
using ShipMate.Domain.Common;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;
using Keys = ShipMate.Domain.Policies.ScopeDisciplinePolicy.Keys;

namespace ShipMate.Tests.Application;

public class ShipServiceTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly IVerificationService _verification = Substitute.For<IVerificationService>();
    private readonly InMemoryAuditTrailRepository _audit = new();
    private readonly IShipAttemptRepository _attempts = Substitute.For<IShipAttemptRepository>();
    private readonly IShipCardRepository _cards = Substitute.For<IShipCardRepository>();
    private readonly IProductionUrlChecker _checker = Substitute.For<IProductionUrlChecker>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly ShipService _service;

    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _manager = Guid.NewGuid();
    private readonly Guid _developer = Guid.NewGuid();
    private readonly Guid _checklistId = Guid.NewGuid();
    private readonly DateTime _verifiedAt = DateTime.UtcNow.AddHours(-2);
    private readonly Workspace _workspace;
    private readonly MvpSnapshot _snapshot;
    private readonly SnapshotFeature _booking = Feature("Booking");
    private readonly SnapshotFeature _records = Feature("Records");
    private readonly SnapshotFeature _replaced = Feature("Chat", SnapshotFeatureStatus.Replaced);
    private readonly SnapshotFeature _backup = Feature("AI triage", SnapshotFeatureStatus.Backup);
    private readonly List<BuildSubmission> _newestSubmissions = new();
    private readonly List<DriftAlert> _openAlerts = new();
    private readonly List<ShipAttempt> _addedAttempts = new();
    private readonly List<ShipCard> _addedCards = new();

    private static readonly ProductionUrlCheckResult Passing = new(200, null, true, null, DateTime.UtcNow.AddDays(60), true, null);

    public ShipServiceTests()
    {
        _service = new ShipService(
            _guard, _workspaces, _definitions, _submissions, _alerts, _verification,
            _audit, new AuditTrail(_audit), _attempts, _cards, _checker, _notifier);

        _workspace = new Workspace
        {
            Id = _workspaceId,
            Name = "Nha Khoa",
            Status = WorkspaceStatus.Active,
            FirstLockedAt = DateTime.UtcNow.AddDays(-5)
        };

        _snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 2 };
        foreach (var feature in new[] { _booking, _records, _replaced, _backup })
        {
            _snapshot.Features.Add(feature);
        }

        _guard.GetWorkspaceAsMemberAsync(Arg.Any<Guid>(), _workspaceId).Returns(_workspace);
        _guard.GetWorkspaceAsManagerAsync(_manager, _workspaceId).Returns(_workspace);
        _guard.GetWorkspaceAsManagerAsync(_developer, _workspaceId).ThrowsAsync(new WorkspacePermissionDeniedException());
        _workspaces.GetCurrentAsync(_workspaceId).Returns(_workspace);
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(_snapshot);
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns(_newestSubmissions);
        _alerts.GetOpenByWorkspaceAsync(_workspaceId).Returns(_openAlerts);
        _verification.GetReadinessAsync(_workspaceId, 2).Returns(Verified());
        _attempts.GetStartTimesSinceAsync(default, default).ReturnsForAnyArgs(new List<DateTime>());
        _checker.CheckAsync(default!, default!, default).ReturnsForAnyArgs(Passing);
        _attempts.When(r => r.AddAsync(Arg.Any<ShipAttempt>())).Do(c => _addedAttempts.Add(c.Arg<ShipAttempt>()));
        _cards.When(r => r.AddAsync(Arg.Any<ShipCard>())).Do(c => _addedCards.Add(c.Arg<ShipCard>()));

        NewestSubmission(_booking, BuildSubmissionStatus.Approved);
        NewestSubmission(_records, BuildSubmissionStatus.Approved);
        Record(AuditTypes.SnapshotLocked, new { version = 1 });
        Record(AuditTypes.VerifyCompleted, new { version = 2 });
    }

    private static SnapshotFeature Feature(string name, SnapshotFeatureStatus status = SnapshotFeatureStatus.Locked) =>
        new() { FeatureId = Guid.NewGuid(), Name = name, Complexity = 2, Status = status };

    private VerificationReadiness Verified() => new(true, true, [], _verifiedAt, _checklistId);

    private void NewestSubmission(SnapshotFeature feature, BuildSubmissionStatus status)
    {
        _newestSubmissions.RemoveAll(s => s.FeatureId == feature.FeatureId);
        _newestSubmissions.Add(new BuildSubmission { WorkspaceId = _workspaceId, FeatureId = feature.FeatureId, Status = status });
    }

    private AuditTrailEntry Record(string type, object? payload = null)
    {
        var entry = new AuditTrailEntry
        {
            WorkspaceId = _workspaceId,
            AuditType = type,
            Payload = JsonSerializer.Serialize(payload ?? new { }, Json)
        };
        AuditChain.Link(entry, _audit.Entries.LastOrDefault());
        _audit.Entries.Add(entry);
        return entry;
    }

    private Task<ShipAttemptResultDto> Ship(Guid? user = null, int version = 2, string url = "https://NhaKhoa.example") =>
        _service.StartAttemptAsync(user ?? _manager, _workspaceId, new StartShipAttemptRequest { SnapshotVersion = version, Url = url });

    private async Task<ShipCard> ShippedCard(bool isPublic = false)
    {
        await Ship();
        var card = _addedCards.Single();
        card.IsPublic = isPublic;
        _cards.GetByIdAsync(_workspaceId, card.Id).Returns(card);
        _cards.GetBySlugAsync(card.PublicSlug).Returns(card);
        return card;
    }

    private async Task NothingWasChecked() =>
        await _checker.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);

    private static ScoreComponent Part(ShipCard card, string key) =>
        JsonSerializer.Deserialize<List<ScoreComponent>>(card.ScoreBreakdown, Json)!.Single(c => c.Key == key);

    private static object? DetailOf(AppException ex, string property) =>
        ex.Details!.GetType().GetProperty(property)!.GetValue(ex.Details);

    // ---- shipping ----

    [Fact]
    public async Task EverythingReady_MintsTheCard()
    {
        var anchor = _audit.Entries.Last();

        var result = await Ship();

        var card = Assert.Single(_addedCards);
        Assert.Equal(ShipAttemptStatus.Shipped, result.Attempt.Status);
        Assert.Null(result.Attempt.FailureCode);
        Assert.Equal(card.Id, result.Card!.Id);
        Assert.Equal(2, card.SnapshotVersion);
        Assert.Equal("Nha Khoa", card.ProductName);
        Assert.Equal("https://nhakhoa.example/", card.Url);
        Assert.Equal(_manager, card.ShippedByUserId);
        Assert.Equal(_addedAttempts.Single().Id, card.ShipAttemptId);
        Assert.Equal(_checklistId, card.VerificationChecklistId);
        Assert.Equal(_verifiedAt, card.VerificationCompletedAt);
        Assert.Equal(100, card.Score);
        Assert.Equal(ScopeDisciplinePolicy.FormulaVersion, card.ScoreFormulaVersion);
        Assert.False(card.IsPublic);
        Assert.False(string.IsNullOrEmpty(card.PublicSlug));
        Assert.Equal(anchor.Sequence, card.AuditAnchorSequence);
        Assert.Equal(anchor.RecordHash, card.AuditAnchorHash);
        await _cards.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task TimeToShip_RunsFromTheFirstLock_NotFromTheVersion()
    {
        await Ship();

        var fiveDays = (long)TimeSpan.FromDays(5).TotalSeconds;
        Assert.InRange(_addedCards.Single().TimeToShipSeconds, fiveDays - 5, fiveDays + 5);
    }

    [Fact]
    public async Task TheCardIsWrittenToTheAuditTrail_AfterItsAnchor()
    {
        await Ship();

        var card = _addedCards.Single();
        var minted = _audit.Entries.Last();
        Assert.Equal(AuditTypes.ShipCardMinted, minted.AuditType);
        Assert.Equal(_manager, minted.ActorUserId);
        Assert.True(minted.Sequence > card.AuditAnchorSequence);
        using var payload = JsonDocument.Parse(minted.Payload);
        Assert.Equal(card.Id, payload.RootElement.GetProperty("cardId").GetGuid());
        Assert.Equal(100, payload.RootElement.GetProperty("score").GetInt32());
        Assert.Equal(7, payload.RootElement.GetProperty("breakdown").GetArrayLength());
    }

    [Fact]
    public async Task TheTeamIsToldTheVersionShipped()
    {
        await Ship();

        await _notifier.Received(1).PublishAsync(_workspaceId, WorkspaceEvents.ShipCardMinted, Arg.Any<object>());
    }

    [Fact]
    public async Task AWorkspaceWithNoAuditRecordYet_AnchorsOnTheStartOfTheChain()
    {
        _audit.Entries.Clear();

        await Ship();

        var card = _addedCards.Single();
        Assert.Equal(0, card.AuditAnchorSequence);
        Assert.Equal(AuditChain.GenesisHash, card.AuditAnchorHash);
    }

    [Fact]
    public async Task TheUrlIsKeptEscaped_SoARecheckCallsTheSameAddress()
    {
        await Ship(url: "https://nhakhoa.example/files/a%2Fb%20c");

        Assert.Equal("https://nhakhoa.example/files/a%2Fb%20c", _addedAttempts.Single().Url);
    }

    [Fact]
    public async Task TheUrlIsCheckedWithTheWorkspacesOwnToken()
    {
        await Ship();

        await _checker.Received(1).CheckAsync(new Uri("https://nhakhoa.example/"), _workspace.ShipOwnershipToken, Arg.Any<CancellationToken>());
    }

    // ---- the score ----

    [Fact]
    public async Task TheScore_CountsTheAuditTrail_AndTheDriftStillOpenOnTheShippedFeatures()
    {
        Record(AuditTypes.ScopeChange, new { action = nameof(ChallengeAction.RemoveAndReplace) });
        Record(AuditTypes.ScopeChange, new { action = nameof(ChallengeAction.AddDirectly) });
        Record(AuditTypes.ScopeChange, new { action = nameof(ChallengeAction.Backup) });
        Record(AuditTypes.ScopeBypass);
        Record(AuditTypes.ScopeBypass);
        Record(AuditTypes.SoloOverrideLogged);
        Record(AuditTypes.GovernanceDilution);
        Record(AuditTypes.ThresholdChanged);
        Record(AuditTypes.DirectPushDetected);
        // Raised in version 1 and still open: the feature is in version 2, so it still counts.
        _openAlerts.Add(new DriftAlert { FeatureId = _booking.FeatureId, SnapshotVersionAtCheck = 1 });
        _openAlerts.Add(new DriftAlert { FeatureId = _records.FeatureId, SnapshotVersionAtCheck = 2 });
        // On a feature no longer in the scope: not shipped, not counted.
        _openAlerts.Add(new DriftAlert { FeatureId = _replaced.FeatureId, SnapshotVersionAtCheck = 1 });
        _workspace.LaunchDeadline = DateTime.UtcNow.Date.AddDays(-3);

        await Ship();

        var card = _addedCards.Single();
        Assert.Equal(9, Part(card, Keys.ScopeVolatility).Deduction);
        Assert.Equal(10, Part(card, Keys.ScopeBypass).Deduction);
        Assert.Equal(10, Part(card, Keys.GovernanceDilution).Deduction);
        Assert.Equal(2, Part(card, Keys.OpenDrift).Count);
        Assert.Equal(5, Part(card, Keys.RemoveAndReplace).Deduction);
        Assert.Equal(2, Part(card, Keys.RemoveAndReplace).OutOf);
        Assert.Equal(3, Part(card, Keys.DeadlineOverrun).Count);
        Assert.Equal(5, Part(card, Keys.DirectPush).Deduction);
        Assert.Equal(100 - (9 + 10 + 10 + 10 + 5 + 3 + 5), card.Score);
    }

    [Fact]
    public async Task TheCardShowsWhereEveryPointWent()
    {
        Record(AuditTypes.ScopeBypass);

        var result = await Ship();

        var bypass = result.Card!.ScoreBreakdown.Single(c => c.Key == Keys.ScopeBypass);
        Assert.Equal(1, bypass.Count);
        Assert.Equal(5, bypass.Deduction);
        Assert.Equal(25, bypass.Cap);
        Assert.Equal(95, result.Card.Score);
        Assert.Equal(100, result.Card.MaxScore);
    }

    // ---- what stops a ship, in order, before any request leaves the server ----

    [Fact]
    public async Task OnlyAManager_Ships()
    {
        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => Ship(_developer));
        await NothingWasChecked();
    }

    [Theory]
    [InlineData("http://nhakhoa.example", "NOT_HTTPS")]
    [InlineData("https://nhakhoa.example:8443", "NOT_DEFAULT_PORT")]
    [InlineData("https://user:pw@nhakhoa.example", "HAS_CREDENTIALS")]
    [InlineData("https://localhost", "LOCAL_HOST")]
    [InlineData("nhakhoa.example", "NOT_ABSOLUTE")]
    public async Task AnAddressThatIsNotAPublicHttpsOne_IsRefused_WithTheReason(string url, string reason)
    {
        var ex = await Assert.ThrowsAsync<ShipUrlInvalidException>(() => Ship(url: url));

        Assert.Equal(reason, DetailOf(ex, "Reason"));
        await NothingWasChecked();
    }

    [Fact]
    public async Task AnArchivedWorkspace_CannotShip()
    {
        _workspace.Status = WorkspaceStatus.Archived;

        await Assert.ThrowsAsync<ShipWorkspaceArchivedException>(() => Ship());
        await NothingWasChecked();
    }

    [Fact]
    public async Task BeforeTheFirstLock_ThereIsNothingToShip()
    {
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        await Assert.ThrowsAsync<ShipNotLockedException>(() => Ship());
    }

    [Fact]
    public async Task AnOlderVersion_CannotShip_AndTheNewestIsNamed()
    {
        var ex = await Assert.ThrowsAsync<ShipVersionNotLatestException>(() => Ship(version: 1));

        Assert.Equal(2, DetailOf(ex, "LatestVersion"));
        await NothingWasChecked();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0)]
    public async Task AVersionThatDoesNotExist_IsNotFound(int version)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Ship(version: version));
    }

    [Fact]
    public async Task AVersionAlreadyShipped_CannotShipAgain()
    {
        _cards.GetByVersionAsync(_workspaceId, 2).Returns(new ShipCard { WorkspaceId = _workspaceId, SnapshotVersion = 2 });

        await Assert.ThrowsAsync<ShipAlreadyShippedException>(() => Ship());
        await NothingWasChecked();
    }

    [Fact]
    public async Task WhileShipMateHasLostTheRepository_NothingShips()
    {
        _workspace.GovernanceSuspendedAt = DateTime.UtcNow;
        _workspace.GovernanceSuspendedReason = Workspace.AppUninstalledReason;

        await Assert.ThrowsAsync<ShipGovernanceSuspendedException>(() => Ship());
        await NothingWasChecked();
    }

    [Fact]
    public async Task ALockedFeatureWhoseNewestCodeIsNotApproved_StopsTheShip_AndIsNamed()
    {
        NewestSubmission(_records, BuildSubmissionStatus.Drift);

        var ex = await Assert.ThrowsAsync<ShipFeaturesNotApprovedException>(() => Ship());

        var features = (IReadOnlyList<ShipFeatureRef>)DetailOf(ex, "Features")!;
        Assert.Equal([new ShipFeatureRef(_records.FeatureId, "Records")], features);
        await NothingWasChecked();
    }

    [Fact]
    public async Task ALockedFeatureThatNeverWentThroughBuild_StopsTheShip()
    {
        // Without this rule a team could skip BUILD entirely and still get a perfect score.
        _newestSubmissions.RemoveAll(s => s.FeatureId == _records.FeatureId);

        await Assert.ThrowsAsync<ShipFeaturesNotApprovedException>(() => Ship());
    }

    [Fact]
    public async Task BackupAndReplacedFeatures_AreNotRequiredToBeApproved()
    {
        // The two locked features are approved; the backup and the replaced one have no code at all.
        await Ship();

        Assert.Single(_addedCards);
    }

    [Fact]
    public async Task AnUnfinishedChecklist_StopsTheShip_AndSaysWhatIsMissing()
    {
        _verification.GetReadinessAsync(_workspaceId, 2).Returns(
            new VerificationReadiness(true, false, [VerificationItemKeys.BasicCrud], null, _checklistId));

        var ex = await Assert.ThrowsAsync<ShipVerifyIncompleteException>(() => Ship());

        Assert.Equal([VerificationItemKeys.BasicCrud], (IReadOnlyList<string>)DetailOf(ex, "MissingItems")!);
        await NothingWasChecked();
    }

    [Fact]
    public async Task AVersionWithoutChecklist_CannotShip()
    {
        _verification.GetReadinessAsync(_workspaceId, 2).Returns(VerificationReadiness.Missing);

        await Assert.ThrowsAsync<ShipVerifyIncompleteException>(() => Ship());
    }

    [Fact]
    public async Task AnAlteredAuditHistory_CannotBeVouchedFor()
    {
        _audit.Entries[0].Payload = """{"version":99}""";

        var ex = await Assert.ThrowsAsync<ShipAuditChainBrokenException>(() => Ship());

        Assert.Equal(1L, DetailOf(ex, "BrokenAtSequence"));
        await NothingWasChecked();
    }

    [Fact]
    public async Task TooManyAttemptsInTheLastHour_WaitBeforeTryingAgain()
    {
        var now = DateTime.UtcNow;
        _attempts.GetStartTimesSinceAsync(_workspaceId, Arg.Any<DateTime>())
            .Returns(Enumerable.Range(1, ShipAttemptLimitPolicy.MaxAttemptsPerWindow).Select(i => now.AddMinutes(-i)).ToList());

        var ex = await Assert.ThrowsAsync<ShipRateLimitedException>(() => Ship());

        Assert.Equal(429, ex.StatusCode);
        await NothingWasChecked();
    }

    // ---- failed checks ----

    [Fact]
    public async Task FailedChecks_AreKeptAsAnAttempt_AndNothingShips()
    {
        _checker.CheckAsync(default!, default!, default).ReturnsForAnyArgs(
            new ProductionUrlCheckResult(200, null, true, null, null, false, ShipCheckCodes.FileNotFound));

        var result = await Ship();

        Assert.Null(result.Card);
        Assert.Equal(ShipAttemptStatus.ChecksFailed, result.Attempt.Status);
        Assert.Equal(ShipCheckCodes.OwnershipUnverified, result.Attempt.FailureCode);
        Assert.True(result.Attempt.Ping.Passed);
        Assert.False(result.Attempt.Ownership.Verified);
        Assert.Equal(ShipCheckCodes.FileNotFound, result.Attempt.Ownership.Error);
        Assert.Single(_addedAttempts);
        await _attempts.Received(1).SaveChangesAsync();
        await _cards.DidNotReceiveWithAnyArgs().AddAsync(default!);
        Assert.DoesNotContain(_audit.Entries, e => e.AuditType == AuditTypes.ShipCardMinted);
    }

    // ---- what changed while the URL was being checked ----

    [Fact]
    public async Task AChecklistItemTakenBackDuringTheChecks_StopsTheCard()
    {
        _verification.GetReadinessAsync(_workspaceId, 2).Returns(
            Verified(),
            new VerificationReadiness(true, false, [VerificationItemKeys.Authentication], null, _checklistId));

        await Assert.ThrowsAsync<ShipVerifyIncompleteException>(() => Ship());

        Assert.Empty(_addedCards);
        await _cards.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ANewVersionLockedDuringTheChecks_StopsTheCard()
    {
        var newer = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 3, Features = _snapshot.Features };
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(_snapshot, newer);
        _verification.GetReadinessAsync(_workspaceId, 3).Returns(VerificationReadiness.Missing);

        await Assert.ThrowsAsync<ShipVersionNotLatestException>(() => Ship());

        Assert.Empty(_addedCards);
    }

    [Fact]
    public async Task AnotherRequestShippedTheSameVersionFirst_ThisOneSaysSo()
    {
        _cards.SaveChangesAsync().ThrowsAsync(new ShipAlreadyShippedException());

        await Assert.ThrowsAsync<ShipAlreadyShippedException>(() => Ship());

        await _notifier.DidNotReceiveWithAnyArgs().PublishAsync(default, default!, default!);
    }

    // ---- trying again ----

    [Fact]
    public async Task Recheck_RunsTheSameVersionAndUrl_AsANewAttempt()
    {
        var earlier = new ShipAttempt
        {
            WorkspaceId = _workspaceId, SnapshotVersion = 2, Url = "https://nhakhoa.example/",
            Status = ShipAttemptStatus.ChecksFailed, FailureCode = ShipCheckCodes.OwnershipUnverified
        };
        _attempts.GetByIdAsync(_workspaceId, earlier.Id).Returns(earlier);

        var result = await _service.RecheckAsync(_manager, _workspaceId, earlier.Id);

        Assert.NotEqual(earlier.Id, result.Attempt.Id);
        Assert.Equal(ShipAttemptStatus.Shipped, result.Attempt.Status);
        Assert.Equal(ShipAttemptStatus.ChecksFailed, earlier.Status);
    }

    [Fact]
    public async Task Recheck_OfAnUnknownAttempt_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RecheckAsync(_manager, _workspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Recheck_IsForTheManagerOnly()
    {
        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.RecheckAsync(_developer, _workspaceId, Guid.NewGuid()));
    }

    // ---- readiness ----

    [Fact]
    public async Task Readiness_AllClear_CanShip()
    {
        var readiness = await _service.GetReadinessAsync(_developer, _workspaceId);

        Assert.True(readiness.CanShip);
        Assert.Empty(readiness.Blockers);
        Assert.Equal(2, readiness.Version);
        Assert.Equal(2, readiness.LockedFeatures);
        Assert.Equal(2, readiness.ApprovedFeatures);
        Assert.Null(readiness.ShipCardId);
    }

    [Fact]
    public async Task Readiness_ListsEverythingInTheWay_AtOnce_InTheOrderAShipChecksThem()
    {
        _workspace.GovernanceSuspendedAt = DateTime.UtcNow;
        NewestSubmission(_records, BuildSubmissionStatus.InScope);
        _verification.GetReadinessAsync(_workspaceId, 2).Returns(
            new VerificationReadiness(true, false, [VerificationItemKeys.ErrorHandling], null, _checklistId));

        var readiness = await _service.GetReadinessAsync(_developer, _workspaceId);

        Assert.False(readiness.CanShip);
        Assert.Equal(["SHIP_GOVERNANCE_SUSPENDED", "SHIP_FEATURES_NOT_APPROVED", "SHIP_VERIFY_INCOMPLETE"], readiness.Blockers);
        Assert.Equal(_records.FeatureId, Assert.Single(readiness.UnapprovedFeatures).FeatureId);
        Assert.Equal(1, readiness.ApprovedFeatures);
        Assert.Equal([VerificationItemKeys.ErrorHandling], readiness.MissingVerificationItems);
        Assert.True(readiness.GovernanceSuspended);
    }

    [Fact]
    public async Task Readiness_BeforeTheFirstLock_HasNoVersion()
    {
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        var readiness = await _service.GetReadinessAsync(_developer, _workspaceId);

        Assert.Null(readiness.Version);
        Assert.Equal(["SHIP_NOT_LOCKED"], readiness.Blockers);
    }

    [Fact]
    public async Task Readiness_OnceShipped_PointsAtTheCard()
    {
        var shipped = new ShipCard { WorkspaceId = _workspaceId, SnapshotVersion = 2 };
        _cards.GetByVersionAsync(_workspaceId, 2).Returns(shipped);

        var readiness = await _service.GetReadinessAsync(_developer, _workspaceId);

        Assert.Equal(["SHIP_ALREADY_SHIPPED"], readiness.Blockers);
        Assert.Equal(shipped.Id, readiness.ShipCardId);
    }

    [Fact]
    public async Task Readiness_ShowsABrokenHistory()
    {
        _audit.Entries[1].Payload = "{}";

        var readiness = await _service.GetReadinessAsync(_developer, _workspaceId);

        Assert.Equal(["SHIP_AUDIT_CHAIN_BROKEN"], readiness.Blockers);
        Assert.Equal(2L, readiness.AuditChainBrokenAt);
    }

    // ---- the ownership token ----

    [Fact]
    public async Task Ownership_GivesTheManagerTheTokenAndWhereToPutIt()
    {
        var ownership = await _service.GetOwnershipAsync(_manager, _workspaceId);

        Assert.Equal(_workspace.ShipOwnershipToken, ownership.Token);
        Assert.Equal("/.well-known/shipmate.txt", ownership.Path);
    }

    [Fact]
    public async Task Ownership_IsForTheManagerOnly()
    {
        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.GetOwnershipAsync(_developer, _workspaceId));
    }

    // ---- verifying a card ----

    [Fact]
    public async Task AFreshCard_IsValid()
    {
        var card = await ShippedCard();

        var check = await _service.VerifyCardAsync(_developer, _workspaceId, card.Id);

        Assert.True(check.Valid);
        Assert.Null(check.Reason);
        Assert.Equal(card.AuditAnchorSequence, check.AnchorSequence);
        Assert.Equal(card.AuditAnchorHash, check.AnchorHash);
    }

    [Fact]
    public async Task HistoryWrittenAfterTheCard_DoesNotAffectIt_EvenIfThatPartIsAltered()
    {
        var card = await ShippedCard();
        Record(AuditTypes.ScopeBypass);
        Record(AuditTypes.ScopeChange, new { action = "AddDirectly" }).Payload = "{}";

        Assert.True((await _service.VerifyCardAsync(_developer, _workspaceId, card.Id)).Valid);
    }

    [Fact]
    public async Task ARecordTheCardCountedAltered_InvalidatesIt()
    {
        var card = await ShippedCard();
        _audit.Entries[0].Payload = """{"version":42}""";

        var check = await _service.VerifyCardAsync(_developer, _workspaceId, card.Id);

        Assert.False(check.Valid);
        Assert.Equal(ShipCardVerificationReasons.AuditChainBroken, check.Reason);
    }

    [Fact]
    public async Task ARecordTheCardCountedRemoved_InvalidatesIt()
    {
        var card = await ShippedCard();
        _audit.Entries.RemoveAt(0);

        Assert.Equal(ShipCardVerificationReasons.AuditChainBroken, (await _service.VerifyCardAsync(_developer, _workspaceId, card.Id)).Reason);
    }

    [Fact]
    public async Task TheCardsScoreEditedInTheDatabase_IsCaught()
    {
        var card = await ShippedCard();
        card.Score = 60;

        var check = await _service.VerifyCardAsync(_developer, _workspaceId, card.Id);

        Assert.False(check.Valid);
        Assert.Equal(ShipCardVerificationReasons.CardRecordMismatch, check.Reason);
    }

    [Fact]
    public async Task TheCardsBreakdownEditedInTheDatabase_IsCaught()
    {
        Record(AuditTypes.ScopeBypass);
        var card = await ShippedCard();
        card.ScoreBreakdown = card.ScoreBreakdown.Replace("\"deduction\":5", "\"deduction\":0");

        Assert.Equal(ShipCardVerificationReasons.CardRecordMismatch, (await _service.VerifyCardAsync(_developer, _workspaceId, card.Id)).Reason);
    }

    [Fact]
    public async Task TheCardsAnchorEditedInTheDatabase_IsCaught()
    {
        var card = await ShippedCard();
        card.AuditAnchorHash = new string('f', 64);

        Assert.Equal(ShipCardVerificationReasons.AnchorMismatch, (await _service.VerifyCardAsync(_developer, _workspaceId, card.Id)).Reason);
    }

    [Fact]
    public async Task ACardWhoseRecordIsGone_IsInvalid()
    {
        var card = await ShippedCard();
        _audit.Entries.RemoveAll(e => e.AuditType == AuditTypes.ShipCardMinted);

        Assert.Equal(ShipCardVerificationReasons.CardRecordMissing, (await _service.VerifyCardAsync(_developer, _workspaceId, card.Id)).Reason);
    }

    // ---- public cards ----

    [Fact]
    public async Task APrivateCard_IsNotFoundPublicly()
    {
        var card = await ShippedCard(isPublic: false);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetPublicCardAsync(card.PublicSlug));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.VerifyPublicCardAsync(card.PublicSlug));
    }

    [Fact]
    public async Task APublicCard_CanBeReadAndVerifiedByAnyone()
    {
        var card = await ShippedCard(isPublic: true);

        var shown = await _service.GetPublicCardAsync(card.PublicSlug);
        var check = await _service.VerifyPublicCardAsync(card.PublicSlug);

        Assert.Equal("Nha Khoa", shown.ProductName);
        Assert.Equal(2, shown.Version);
        Assert.Equal(card.Score, shown.Score);
        Assert.Equal(7, shown.ScoreBreakdown.Count);
        Assert.True(check.Valid);
    }

    [Fact]
    public async Task AnUnknownSlug_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetPublicCardAsync("nope"));
    }

    [Fact]
    public async Task TheManager_MakesACardPublic()
    {
        var card = await ShippedCard();
        _cards.ClearReceivedCalls();

        var result = await _service.SetVisibilityAsync(_manager, _workspaceId, card.Id, new SetShipCardVisibilityRequest { IsPublic = true });

        Assert.True(result.IsPublic);
        Assert.True(card.IsPublic);
        await _cards.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task SettingTheSameVisibility_SavesNothing()
    {
        var card = await ShippedCard();
        _cards.ClearReceivedCalls();

        await _service.SetVisibilityAsync(_manager, _workspaceId, card.Id, new SetShipCardVisibilityRequest { IsPublic = false });

        await _cards.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ADeveloper_CannotChangeVisibility()
    {
        var card = await ShippedCard();

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() =>
            _service.SetVisibilityAsync(_developer, _workspaceId, card.Id, new SetShipCardVisibilityRequest { IsPublic = true }));
    }

    [Fact]
    public async Task MakingACardPublic_DoesNotChangeWhatItVouchesFor()
    {
        var card = await ShippedCard();

        await _service.SetVisibilityAsync(_manager, _workspaceId, card.Id, new SetShipCardVisibilityRequest { IsPublic = true });

        Assert.True((await _service.VerifyPublicCardAsync(card.PublicSlug)).Valid);
    }

    // ---- history ----

    [Fact]
    public async Task Attempts_AreListedForEveryMember_WithEachCheck()
    {
        _attempts.GetRecentAsync(_workspaceId, Arg.Any<int>()).Returns(new List<ShipAttempt>
        {
            new()
            {
                WorkspaceId = _workspaceId, SnapshotVersion = 2, Url = "https://nhakhoa.example/",
                Status = ShipAttemptStatus.ChecksFailed, FailureCode = ShipCheckCodes.PingFailed,
                PingStatusCode = 503, PingError = ShipCheckCodes.UnexpectedStatus, SslValid = true
            }
        });

        var attempt = Assert.Single(await _service.GetAttemptsAsync(_developer, _workspaceId));

        Assert.False(attempt.Ping.Passed);
        Assert.Equal(503, attempt.Ping.StatusCode);
        Assert.True(attempt.Ssl.Valid);
    }
}

public class StartShipAttemptRequestValidatorTests
{
    private readonly StartShipAttemptRequestValidator _validator = new();

    [Fact]
    public void AVersionAndAUrl_AreValid()
    {
        Assert.True(_validator.Validate(new StartShipAttemptRequest { SnapshotVersion = 1, Url = "https://a.example" }).IsValid);
    }

    [Theory]
    [InlineData(0, "https://a.example")]
    [InlineData(-1, "https://a.example")]
    [InlineData(1, "")]
    public void AMissingVersionOrUrl_IsInvalid(int version, string url)
    {
        Assert.False(_validator.Validate(new StartShipAttemptRequest { SnapshotVersion = version, Url = url }).IsValid);
    }

    [Fact]
    public void ATooLongUrl_IsInvalid()
    {
        var url = "https://a.example/" + new string('a', ProductionUrlPolicy.MaxLength);

        Assert.False(_validator.Validate(new StartShipAttemptRequest { SnapshotVersion = 1, Url = url }).IsValid);
    }
}
