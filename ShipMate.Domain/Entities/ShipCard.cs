using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

/// <summary>
/// Proof that a snapshot version ran in production, with the two scores SHIP computes. Everything on it is copied at
/// the moment of shipping and never changes, except whether it is public. One card per version.
/// </summary>
public class ShipCard : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public int SnapshotVersion { get; set; }
    public Guid ShipAttemptId { get; set; }

    // The workspace's name when it shipped: renaming the workspace later doesn't rewrite the card.
    public string ProductName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
    public Guid ShippedByUserId { get; set; }

    // From the workspace's first Lock, so a late Feature Challenge can't restart the clock.
    public long TimeToShipSeconds { get; set; }

    public int Score { get; set; }
    public string ScoreFormulaVersion { get; set; } = string.Empty;

    // JSON list of ScoreComponent: what each part counted and took off, so the card explains its own score.
    public string ScoreBreakdown { get; set; } = "[]";

    public Guid VerificationChecklistId { get; set; }
    public DateTime VerificationCompletedAt { get; set; }

    // The last audit record when the card was minted. The score counts records up to it, and the card stays valid
    // only while the chain up to it is unchanged. Sequence 0 means the workspace had no record yet.
    public long AuditAnchorSequence { get; set; }
    public string AuditAnchorHash { get; set; } = string.Empty;

    public bool IsPublic { get; set; }

    // Random, so a public card can't be found by guessing; it exists from the start and is only served while public.
    public string PublicSlug { get; set; } = string.Empty;
}
