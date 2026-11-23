using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Ship;

public class StartShipAttemptRequest
{
    // The version the Manager means to ship. It must be the newest one, so a version that changed while the page
    // was open is refused instead of shipping something nobody looked at.
    public int SnapshotVersion { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class SetShipCardVisibilityRequest
{
    public bool IsPublic { get; set; }
}

public class ShipFeatureDto
{
    public Guid FeatureId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Whether the newest version can be shipped now and, if not, everything that stands in the way.</summary>
public class ShipReadinessDto
{
    // Null before the first Lock.
    public int? Version { get; set; }
    public bool CanShip { get; set; }

    // Every reason it can't, in the order they are checked (the error codes of a ship attempt).
    public List<string> Blockers { get; set; } = new();

    public int LockedFeatures { get; set; }
    public int ApprovedFeatures { get; set; }
    public List<ShipFeatureDto> UnapprovedFeatures { get; set; } = new();
    public List<string> MissingVerificationItems { get; set; } = new();
    public bool GovernanceSuspended { get; set; }
    public long? AuditChainBrokenAt { get; set; }

    // Set once the version is shipped.
    public Guid? ShipCardId { get; set; }
}

public class ShipOwnershipDto
{
    public string Token { get; set; } = string.Empty;

    // Where on the production host the token must be served, as plain text.
    public string Path { get; set; } = string.Empty;
}

public class PingCheckDto
{
    public bool Passed { get; set; }
    public int? StatusCode { get; set; }
    public string? Error { get; set; }
}

public class SslCheckDto
{
    public bool Valid { get; set; }
    public string? Error { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class OwnershipCheckDto
{
    public bool Verified { get; set; }
    public string? Error { get; set; }
}

public class ShipAttemptDto
{
    public Guid Id { get; set; }
    public int SnapshotVersion { get; set; }
    public string Url { get; set; } = string.Empty;
    public ShipAttemptStatus Status { get; set; }
    public string? FailureCode { get; set; }
    public PingCheckDto Ping { get; set; } = new();
    public SslCheckDto Ssl { get; set; } = new();
    public OwnershipCheckDto Ownership { get; set; } = new();
    public Guid CreatedByUserId { get; set; }
    public DateTime CheckedAt { get; set; }
}

/// <summary>The outcome of a ship attempt: the checks, and the card when they all passed.</summary>
public class ShipAttemptResultDto
{
    public ShipAttemptDto Attempt { get; set; } = new();
    public ShipCardDto? Card { get; set; }
}

public class ScoreComponentDto
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Deduction { get; set; }
    public int Cap { get; set; }

    // Only for a share: the count it is a share of.
    public int? OutOf { get; set; }
}

/// <summary>What anyone may see on a card, public or not.</summary>
public class PublicShipCardDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
    public long TimeToShipSeconds { get; set; }
    public int Score { get; set; }
    public int MaxScore { get; set; }
    public string ScoreFormulaVersion { get; set; } = string.Empty;
    public List<ScoreComponentDto> ScoreBreakdown { get; set; } = new();
    public DateTime VerificationCompletedAt { get; set; }
    public long AuditAnchorSequence { get; set; }
    public string AuditAnchorHash { get; set; } = string.Empty;
}

/// <summary>The card as the team sees it.</summary>
public class ShipCardDto : PublicShipCardDto
{
    public Guid Id { get; set; }
    public Guid ShipAttemptId { get; set; }
    public Guid ShippedByUserId { get; set; }
    public Guid VerificationChecklistId { get; set; }
    public bool IsPublic { get; set; }

    // Always set, but the public address only answers while the card is public.
    public string PublicSlug { get; set; } = string.Empty;
}

/// <summary>Whether the card still matches the audit history it was minted on. Says nothing else about that history.</summary>
public class ShipCardVerificationDto
{
    public bool Valid { get; set; }

    // Null when valid; otherwise one of ShipCardVerificationReasons.
    public string? Reason { get; set; }

    public long AnchorSequence { get; set; }
    public string AnchorHash { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}

public static class ShipCardVerificationReasons
{
    // A record up to the card's own was changed or removed.
    public const string AuditChainBroken = "AUDIT_CHAIN_BROKEN";

    // The record the card anchors on is no longer the one it saw.
    public const string AnchorMismatch = "ANCHOR_MISMATCH";

    // The card's own record is gone, or the card no longer says what was recorded when it was minted.
    public const string CardRecordMissing = "CARD_RECORD_MISSING";
    public const string CardRecordMismatch = "CARD_RECORD_MISMATCH";
}
