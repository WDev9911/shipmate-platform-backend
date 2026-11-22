using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// One run of the production URL checks for a snapshot version. Every run is kept, passed or not, so the team sees
/// what failed and the number of runs in the last hour can be limited. Trying again is a new run, never an edit.
/// </summary>
public class ShipAttempt : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public int SnapshotVersion { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Normalized: https only, no credentials, no fragment.
    public string Url { get; set; } = string.Empty;

    public ShipAttemptStatus Status { get; set; }

    // The first check that failed (one of ShipCheckCodes); null when every check passed.
    public string? FailureCode { get; set; }

    // The status of the last response after redirects, or why no response came.
    public int? PingStatusCode { get; set; }
    public string? PingError { get; set; }

    public bool SslValid { get; set; }
    public string? SslError { get; set; }
    public DateTime? SslExpiresAt { get; set; }

    public bool OwnershipVerified { get; set; }
    public string? OwnershipError { get; set; }

    public DateTime CheckedAt { get; set; }
}
