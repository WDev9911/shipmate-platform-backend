using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

public class Workspace : BaseEntity
{
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string VisionPrompt { get; set; } = string.Empty;
    public DateTime? LaunchDeadline { get; set; }
    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Active;

    // Set later via the GitHub repo linking flow (not built yet) — columns exist now so that
    // flow won't need its own migration when it's built.
    public string? GitHubRepoOwner { get; set; }
    public string? GitHubRepoName { get; set; }
    public string? WebhookSecretEncrypted { get; set; }
    public decimal DriftConfidenceThreshold { get; set; } = 0.75m;

    // Set exactly once, by LOCK, on this workspace's first-ever Lock. SHIP uses it to compute
    // Time-to-Ship — must never be overwritten by later Locks/Feature Challenges.
    public DateTime? FirstLockedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
