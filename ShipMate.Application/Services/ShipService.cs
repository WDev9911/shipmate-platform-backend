using System.Security.Cryptography;
using System.Text.Json;
using ShipMate.Application.DTOs.Ship;
using ShipMate.Application.DTOs.Verify;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class ShipService : IShipService
{
    private const int AttemptHistoryLimit = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IBuildSubmissionRepository _submissionRepository;
    private readonly IDriftAlertRepository _alertRepository;
    private readonly IVerificationService _verificationService;
    private readonly IAuditTrailRepository _auditTrailRepository;
    private readonly IAuditTrail _auditTrail;
    private readonly IShipAttemptRepository _attemptRepository;
    private readonly IShipCardRepository _cardRepository;
    private readonly IProductionUrlChecker _urlChecker;
    private readonly IWorkspaceNotifier _notifier;

    public ShipService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IWorkspaceRepository workspaceRepository,
        IProductDefinitionRepository productDefinitionRepository,
        IBuildSubmissionRepository submissionRepository,
        IDriftAlertRepository alertRepository,
        IVerificationService verificationService,
        IAuditTrailRepository auditTrailRepository,
        IAuditTrail auditTrail,
        IShipAttemptRepository attemptRepository,
        IShipCardRepository cardRepository,
        IProductionUrlChecker urlChecker,
        IWorkspaceNotifier notifier)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _workspaceRepository = workspaceRepository;
        _productDefinitionRepository = productDefinitionRepository;
        _submissionRepository = submissionRepository;
        _alertRepository = alertRepository;
        _verificationService = verificationService;
        _auditTrailRepository = auditTrailRepository;
        _auditTrail = auditTrail;
        _attemptRepository = attemptRepository;
        _cardRepository = cardRepository;
        _urlChecker = urlChecker;
        _notifier = notifier;
    }

    // ---- before shipping ----

    public async Task<ShipReadinessDto> GetReadinessAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var state = await LoadStateAsync(workspaceId);
        var approved = state.Locked.Count - state.Unapproved.Count;

        return new ShipReadinessDto
        {
            Version = state.Snapshot?.Version,
            CanShip = state.Blockers.Count == 0,
            Blockers = state.Blockers.ToList(),
            LockedFeatures = state.Locked.Count,
            ApprovedFeatures = approved,
            UnapprovedFeatures = state.Unapproved
                .Select(f => new ShipFeatureDto { FeatureId = f.FeatureId, Name = f.Name })
                .ToList(),
            MissingVerificationItems = state.Verification.MissingItems.ToList(),
            GovernanceSuspended = state.Workspace.GovernanceSuspendedAt is not null,
            AuditChainBrokenAt = state.BrokenAt,
            ShipCardId = state.Card?.Id
        };
    }

    public async Task<ShipOwnershipDto> GetOwnershipAsync(Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        return new ShipOwnershipDto { Token = workspace.ShipOwnershipToken, Path = ShipOwnership.FilePath };
    }

    // ---- shipping ----

    public async Task<ShipAttemptResultDto> StartAttemptAsync(
        Guid userId, Guid workspaceId, StartShipAttemptRequest request, CancellationToken cancellationToken = default)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        return await RunAttemptAsync(workspaceId, userId, request.SnapshotVersion, NormalizeOrThrow(request.Url), cancellationToken);
    }

    public async Task<ShipAttemptResultDto> RecheckAsync(
        Guid userId, Guid workspaceId, Guid attemptId, CancellationToken cancellationToken = default)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var earlier = await _attemptRepository.GetByIdAsync(workspaceId, attemptId)
            ?? throw new NotFoundException("Ship attempt", attemptId);

        // A new attempt, not an edit of the old one: the history keeps every check that ran.
        return await RunAttemptAsync(workspaceId, userId, earlier.SnapshotVersion, NormalizeOrThrow(earlier.Url), cancellationToken);
    }

    private async Task<ShipAttemptResultDto> RunAttemptAsync(
        Guid workspaceId, Guid userId, int version, Uri url, CancellationToken cancellationToken)
    {
        // Refused before any request leaves the server, so nothing that can't ship costs an attempt.
        var before = await LoadStateAsync(workspaceId);
        ThrowIfCannotShip(before, version);

        var startedAt = DateTime.UtcNow;
        var recent = await _attemptRepository.GetStartTimesSinceAsync(workspaceId, startedAt - ShipAttemptLimitPolicy.Window);
        if (ShipAttemptLimitPolicy.RetryAfter(recent, startedAt) is { } retryAfter)
        {
            throw new ShipRateLimitedException(retryAfter);
        }

        var check = await _urlChecker.CheckAsync(url, before.Workspace.ShipOwnershipToken, cancellationToken);
        var attempt = new ShipAttempt
        {
            WorkspaceId = workspaceId,
            SnapshotVersion = version,
            CreatedByUserId = userId,
            CreatedAt = startedAt,
            // AbsoluteUri keeps the escaping (ToString would turn %2F into /), so a recheck calls exactly this address.
            Url = url.AbsoluteUri,
            Status = ShipAttemptStatus.ChecksFailed,
            FailureCode = check.FailureCode,
            PingStatusCode = check.PingStatusCode,
            PingError = check.PingError,
            SslValid = check.SslValid,
            SslError = check.SslError,
            SslExpiresAt = check.SslExpiresAt,
            OwnershipVerified = check.OwnershipVerified,
            OwnershipError = check.OwnershipError,
            CheckedAt = DateTime.UtcNow
        };

        if (check.FailureCode is not null)
        {
            await _attemptRepository.AddAsync(attempt);
            await _attemptRepository.SaveChangesAsync();
            return new ShipAttemptResultDto { Attempt = ToDto(attempt) };
        }

        // The checks can take half a minute. Whatever allowed shipping must still be true now: a new version, a
        // check taken back on the list or lost access to the repository all stop the card.
        var now = await LoadStateAsync(workspaceId);
        ThrowIfCannotShip(now, version);

        attempt.Status = ShipAttemptStatus.Shipped;
        var card = await MintAsync(now, attempt, userId);

        await _attemptRepository.AddAsync(attempt);
        await _cardRepository.AddAsync(card);
        await _auditTrail.AppendAsync(workspaceId, AuditTypes.ShipCardMinted, userId, MintedRecord.Of(card));

        // The attempt, the card and its audit record are saved together; a card minted for the same version by a
        // request that ran at the same time makes this save fail instead of leaving two cards.
        await _cardRepository.SaveChangesAsync();

        await _notifier.PublishAsync(workspaceId, WorkspaceEvents.ShipCardMinted, new
        {
            cardId = card.Id,
            version = card.SnapshotVersion,
            score = card.Score,
            timeToShipSeconds = card.TimeToShipSeconds
        });

        return new ShipAttemptResultDto { Attempt = ToDto(attempt), Card = ToDto(card) };
    }

    private async Task<ShipCard> MintAsync(ShipState state, ShipAttempt attempt, Guid userId)
    {
        var workspace = state.Workspace;
        var snapshot = state.Snapshot!;

        // Set by the first Lock and never cleared, so a snapshot without it means the data is broken. No guessing.
        var firstLockedAt = workspace.FirstLockedAt
            ?? throw new InvalidOperationException($"Workspace {workspace.Id} has a snapshot but no first Lock time.");

        var shippedAt = DateTime.UtcNow;

        // The score counts the history up to this record, and the card stays valid only while that part is unchanged.
        var anchor = state.Chain.LastOrDefault();

        var lockedIds = state.Locked.Select(f => f.FeatureId).ToHashSet();
        var openDrift = (await _alertRepository.GetOpenByWorkspaceAsync(workspace.Id))
            .Count(a => lockedIds.Contains(a.FeatureId));

        var score = ScopeDisciplinePolicy.Compute(
            CountAuditEvents(state.Chain) with
            {
                OpenDrift = openDrift,
                DaysLate = ScopeDisciplinePolicy.DaysLate(workspace.LaunchDeadline, shippedAt)
            });

        return new ShipCard
        {
            WorkspaceId = workspace.Id,
            SnapshotVersion = snapshot.Version,
            ShipAttemptId = attempt.Id,
            ProductName = workspace.Name,
            Url = attempt.Url,
            ShippedAt = shippedAt,
            ShippedByUserId = userId,
            TimeToShipSeconds = Math.Max(0, (long)(shippedAt - firstLockedAt).TotalSeconds),
            Score = score.Score,
            ScoreFormulaVersion = score.FormulaVersion,
            ScoreBreakdown = JsonSerializer.Serialize(score.Components, JsonOptions),
            VerificationChecklistId = state.Verification.ChecklistId!.Value,
            VerificationCompletedAt = state.Verification.CompletedAt!.Value,
            AuditAnchorSequence = anchor?.Sequence ?? 0,
            AuditAnchorHash = anchor?.RecordHash ?? AuditChain.GenesisHash,
            PublicSlug = NewSlug()
        };
    }

    // Every count comes from the audit trail, which can't be edited unnoticed, and stops at the anchor.
    private static ScopeDisciplineCounts CountAuditEvents(IEnumerable<AuditTrailEntry> chain)
    {
        int scopeChanges = 0, removeAndReplace = 0, addDirectly = 0, bypasses = 0, dilutions = 0, directPushes = 0;

        foreach (var entry in chain)
        {
            switch (entry.AuditType)
            {
                case AuditTypes.ScopeChange:
                    scopeChanges++;
                    var action = ReadString(entry.Payload, "action");
                    if (action == nameof(ChallengeAction.RemoveAndReplace))
                    {
                        removeAndReplace++;
                    }
                    else if (action == nameof(ChallengeAction.AddDirectly))
                    {
                        addDirectly++;
                    }

                    break;
                case AuditTypes.ScopeBypass:
                    bypasses++;
                    break;
                case AuditTypes.GovernanceDilution:
                    dilutions++;
                    break;
                case AuditTypes.DirectPushDetected:
                    directPushes++;
                    break;
            }
        }

        return new ScopeDisciplineCounts(
            scopeChanges, removeAndReplace, addDirectly, bypasses, dilutions,
            OpenDrift: 0, DaysLate: 0, directPushes);
    }

    // ---- what stands in the way ----

    private sealed class ShipState
    {
        public required Workspace Workspace { get; init; }
        public MvpSnapshot? Snapshot { get; init; }
        public ShipCard? Card { get; init; }
        public List<SnapshotFeature> Locked { get; init; } = new();
        public List<SnapshotFeature> Unapproved { get; init; } = new();
        public VerificationReadiness Verification { get; init; } = VerificationReadiness.Missing;
        public List<AuditTrailEntry> Chain { get; init; } = new();
        public long? BrokenAt { get; init; }
        public List<string> Blockers { get; } = new();
    }

    // Read fresh every time: the second read, right before minting, must see what changed during the URL checks.
    private async Task<ShipState> LoadStateAsync(Guid workspaceId)
    {
        var workspace = await _workspaceRepository.GetCurrentAsync(workspaceId)
            ?? throw new NotFoundException("Workspace", workspaceId);

        var snapshot = await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId);
        if (snapshot is null)
        {
            var unlocked = new ShipState { Workspace = workspace };
            AddBlockers(unlocked);
            return unlocked;
        }

        var locked = snapshot.Features
            .Where(f => f.Status == SnapshotFeatureStatus.Locked)
            .OrderBy(f => f.Name)
            .ToList();

        // "Approved" means the same here as for VERIFY and the Next Best Action: the newest submission was approved.
        var newest = (await _submissionRepository.GetLatestPerFeatureAsync(workspaceId))
            .GroupBy(s => s.FeatureId)
            .ToDictionary(g => g.Key, g => g.MaxBy(s => s.CreatedAt)!);
        var unapproved = locked
            .Where(f => !newest.TryGetValue(f.FeatureId, out var s) || s.Status != BuildSubmissionStatus.Approved)
            .ToList();

        var chain = await _auditTrailRepository.GetChainAsync(workspaceId);

        var state = new ShipState
        {
            Workspace = workspace,
            Snapshot = snapshot,
            Card = await _cardRepository.GetByVersionAsync(workspaceId, snapshot.Version),
            Locked = locked,
            Unapproved = unapproved,
            Verification = await _verificationService.GetReadinessAsync(workspaceId, snapshot.Version),
            Chain = chain,
            BrokenAt = AuditChain.FindFirstBroken(chain)
        };
        AddBlockers(state);
        return state;
    }

    // In the order they are checked when shipping, so the first one is the error an attempt gets.
    private static void AddBlockers(ShipState state)
    {
        if (state.Workspace.Status == WorkspaceStatus.Archived)
        {
            state.Blockers.Add("SHIP_WORKSPACE_ARCHIVED");
        }

        if (state.Snapshot is null)
        {
            state.Blockers.Add("SHIP_NOT_LOCKED");
            return;
        }

        if (state.Card is not null)
        {
            state.Blockers.Add("SHIP_ALREADY_SHIPPED");
        }

        if (state.Workspace.GovernanceSuspendedAt is not null)
        {
            state.Blockers.Add("SHIP_GOVERNANCE_SUSPENDED");
        }

        // Code that never went through BUILD was never checked against the scope; shipping it would make the
        // card vouch for work ShipMate never saw.
        if (state.Locked.Count == 0 || state.Unapproved.Count > 0)
        {
            state.Blockers.Add("SHIP_FEATURES_NOT_APPROVED");
        }

        if (!state.Verification.Exists || !state.Verification.AllItemsChecked)
        {
            state.Blockers.Add("SHIP_VERIFY_INCOMPLETE");
        }

        // A card anchors on this history; if it was already altered, there is nothing sound to anchor on.
        if (state.BrokenAt is not null)
        {
            state.Blockers.Add("SHIP_AUDIT_CHAIN_BROKEN");
        }
    }

    private static void ThrowIfCannotShip(ShipState state, int requestedVersion)
    {
        if (state.Workspace.Status == WorkspaceStatus.Archived)
        {
            throw new ShipWorkspaceArchivedException();
        }

        var snapshot = state.Snapshot ?? throw new ShipNotLockedException();

        // Versions are numbered 1, 2, 3... with no gaps, so anything below the newest exists.
        if (requestedVersion < 1 || requestedVersion > snapshot.Version)
        {
            throw new NotFoundException("Snapshot version", requestedVersion);
        }

        // Only what the team is committed to now can ship: an older scope was replaced by a Feature Challenge.
        if (requestedVersion < snapshot.Version)
        {
            throw new ShipVersionNotLatestException(snapshot.Version);
        }

        if (state.Card is not null)
        {
            throw new ShipAlreadyShippedException();
        }

        if (state.Workspace.GovernanceSuspendedAt is not null)
        {
            throw new ShipGovernanceSuspendedException();
        }

        if (state.Locked.Count == 0 || state.Unapproved.Count > 0)
        {
            throw new ShipFeaturesNotApprovedException(
                state.Unapproved.Select(f => new ShipFeatureRef(f.FeatureId, f.Name)).ToList());
        }

        if (!state.Verification.Exists || !state.Verification.AllItemsChecked)
        {
            throw new ShipVerifyIncompleteException(state.Verification.MissingItems);
        }

        if (state.BrokenAt is { } brokenAt)
        {
            throw new ShipAuditChainBrokenException(brokenAt);
        }
    }

    // ---- after shipping ----

    public async Task<List<ShipAttemptDto>> GetAttemptsAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        return (await _attemptRepository.GetRecentAsync(workspaceId, AttemptHistoryLimit)).Select(ToDto).ToList();
    }

    public async Task<List<ShipCardDto>> GetCardsAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        return (await _cardRepository.GetByWorkspaceIdAsync(workspaceId)).Select(ToDto).ToList();
    }

    public async Task<ShipCardDto> GetCardAsync(Guid userId, Guid workspaceId, Guid cardId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        return ToDto(await GetCardOrThrow(workspaceId, cardId));
    }

    public async Task<ShipCardVerificationDto> VerifyCardAsync(Guid userId, Guid workspaceId, Guid cardId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        return await VerifyAsync(await GetCardOrThrow(workspaceId, cardId));
    }

    public async Task<ShipCardDto> SetVisibilityAsync(
        Guid userId, Guid workspaceId, Guid cardId, SetShipCardVisibilityRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var card = await GetCardOrThrow(workspaceId, cardId);
        if (card.IsPublic != request.IsPublic)
        {
            card.IsPublic = request.IsPublic;
            await _cardRepository.SaveChangesAsync();
        }

        return ToDto(card);
    }

    public async Task<PublicShipCardDto> GetPublicCardAsync(string publicSlug) =>
        ToPublicDto(await GetPublicCardOrThrow(publicSlug));

    public async Task<ShipCardVerificationDto> VerifyPublicCardAsync(string publicSlug) =>
        await VerifyAsync(await GetPublicCardOrThrow(publicSlug));

    private async Task<ShipCard> GetCardOrThrow(Guid workspaceId, Guid cardId) =>
        await _cardRepository.GetByIdAsync(workspaceId, cardId)
            ?? throw new NotFoundException("Ship card", cardId);

    // A private card answers exactly like one that doesn't exist, so its address reveals nothing.
    private async Task<ShipCard> GetPublicCardOrThrow(string publicSlug)
    {
        var card = await _cardRepository.GetBySlugAsync(publicSlug);
        return card is { IsPublic: true } ? card : throw new NotFoundException("Ship card", publicSlug);
    }

    /// <summary>
    /// The card holds when the history up to its own record is intact, the record it anchors on is the one it saw,
    /// and it still says what was recorded when it was minted. Nothing of the history itself is returned.
    /// </summary>
    private async Task<ShipCardVerificationDto> VerifyAsync(ShipCard card)
    {
        var chain = await _auditTrailRepository.GetChainAsync(card.WorkspaceId);
        var result = new ShipCardVerificationDto
        {
            AnchorSequence = card.AuditAnchorSequence,
            AnchorHash = card.AuditAnchorHash,
            CheckedAt = DateTime.UtcNow
        };

        var minted = chain.FirstOrDefault(e => e.AuditType == AuditTypes.ShipCardMinted && MintedRecord.Read(e.Payload)?.CardId == card.Id);
        if (minted is null)
        {
            return Invalid(result, ShipCardVerificationReasons.CardRecordMissing);
        }

        // What was added after the card can't change what the card vouched for.
        if (AuditChain.FindFirstBroken(chain) is { } broken && broken <= minted.Sequence)
        {
            return Invalid(result, ShipCardVerificationReasons.AuditChainBroken);
        }

        var anchorHash = card.AuditAnchorSequence == 0
            ? AuditChain.GenesisHash
            : chain.FirstOrDefault(e => e.Sequence == card.AuditAnchorSequence)?.RecordHash;
        if (anchorHash != card.AuditAnchorHash)
        {
            return Invalid(result, ShipCardVerificationReasons.AnchorMismatch);
        }

        if (!MintedRecord.Read(minted.Payload)!.Describes(card))
        {
            return Invalid(result, ShipCardVerificationReasons.CardRecordMismatch);
        }

        result.Valid = true;
        return result;
    }

    private static ShipCardVerificationDto Invalid(ShipCardVerificationDto result, string reason)
    {
        result.Valid = false;
        result.Reason = reason;
        return result;
    }

    /// <summary>
    /// The audit record of a minted card: everything on the card a reader relies on. Being in the hash chain, it
    /// makes the card itself tamper-evident, not only the history the card counted.
    /// </summary>
    private sealed record MintedRecord(
        Guid CardId,
        int Version,
        string Url,
        int Score,
        string FormulaVersion,
        long TimeToShipSeconds,
        long AnchorSequence,
        string AnchorHash,
        IReadOnlyList<ScoreComponent> Breakdown)
    {
        public static MintedRecord Of(ShipCard card) => new(
            card.Id, card.SnapshotVersion, card.Url, card.Score, card.ScoreFormulaVersion,
            card.TimeToShipSeconds, card.AuditAnchorSequence, card.AuditAnchorHash, ReadBreakdown(card.ScoreBreakdown));

        public bool Describes(ShipCard card)
        {
            var current = Of(card);
            return CardId == current.CardId
                && Version == current.Version
                && Url == current.Url
                && Score == current.Score
                && FormulaVersion == current.FormulaVersion
                && TimeToShipSeconds == current.TimeToShipSeconds
                && AnchorSequence == current.AnchorSequence
                && AnchorHash == current.AnchorHash
                && (Breakdown ?? []).SequenceEqual(current.Breakdown);
        }

        public static MintedRecord? Read(string payload)
        {
            try
            {
                return JsonSerializer.Deserialize<MintedRecord>(payload, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    // ---- mapping ----

    private static ShipAttemptDto ToDto(ShipAttempt attempt) => new()
    {
        Id = attempt.Id,
        SnapshotVersion = attempt.SnapshotVersion,
        Url = attempt.Url,
        Status = attempt.Status,
        FailureCode = attempt.FailureCode,
        Ping = new PingCheckDto
        {
            Passed = attempt.PingError is null && attempt.PingStatusCode == 200,
            StatusCode = attempt.PingStatusCode,
            Error = attempt.PingError
        },
        Ssl = new SslCheckDto { Valid = attempt.SslValid, Error = attempt.SslError, ExpiresAt = attempt.SslExpiresAt },
        Ownership = new OwnershipCheckDto { Verified = attempt.OwnershipVerified, Error = attempt.OwnershipError },
        CreatedByUserId = attempt.CreatedByUserId,
        CheckedAt = attempt.CheckedAt
    };

    private static ShipCardDto ToDto(ShipCard card)
    {
        var dto = new ShipCardDto
        {
            Id = card.Id,
            ShipAttemptId = card.ShipAttemptId,
            ShippedByUserId = card.ShippedByUserId,
            VerificationChecklistId = card.VerificationChecklistId,
            IsPublic = card.IsPublic,
            PublicSlug = card.PublicSlug
        };
        FillPublic(dto, card);
        return dto;
    }

    private static PublicShipCardDto ToPublicDto(ShipCard card)
    {
        var dto = new PublicShipCardDto();
        FillPublic(dto, card);
        return dto;
    }

    private static void FillPublic(PublicShipCardDto dto, ShipCard card)
    {
        dto.ProductName = card.ProductName;
        dto.Version = card.SnapshotVersion;
        dto.Url = card.Url;
        dto.ShippedAt = card.ShippedAt;
        dto.TimeToShipSeconds = card.TimeToShipSeconds;
        dto.Score = card.Score;
        dto.MaxScore = ScopeDisciplinePolicy.MaxScore;
        dto.ScoreFormulaVersion = card.ScoreFormulaVersion;
        dto.ScoreBreakdown = ReadBreakdown(card.ScoreBreakdown)
            .Select(c => new ScoreComponentDto { Key = c.Key, Count = c.Count, Deduction = c.Deduction, Cap = c.Cap, OutOf = c.OutOf })
            .ToList();
        dto.VerificationCompletedAt = card.VerificationCompletedAt;
        dto.AuditAnchorSequence = card.AuditAnchorSequence;
        dto.AuditAnchorHash = card.AuditAnchorHash;
    }

    // ---- helpers ----

    private static Uri NormalizeOrThrow(string url)
    {
        var error = ProductionUrlPolicy.TryNormalize(url, out var normalized);
        return error == ProductionUrlError.None
            ? normalized!
            : throw new ShipUrlInvalidException(ToCode(error.ToString()));
    }

    // "NotDefaultPort" -> "NOT_DEFAULT_PORT", the way every other code the API returns is written.
    private static string ToCode(string name) =>
        string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpperInvariant();

    private static List<ScoreComponent> ReadBreakdown(string json) =>
        JsonSerializer.Deserialize<List<ScoreComponent>>(json, JsonOptions) ?? new List<ScoreComponent>();

    private static string? ReadString(string payload, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Long and random enough that public cards can't be found by trying addresses.
    private static string NewSlug() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
