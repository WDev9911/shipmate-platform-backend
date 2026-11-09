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

    // The ShipMate GitHub App installation on the linked repo. The Bot works through it, so nothing
    // can be created or protected on GitHub until it is set.
    public long? GitHubInstallationId { get; set; }

    // Branch Protection that limits writing to the Bot only exists on repositories owned by an organization.
    public bool GitHubRepoIsOrganization { get; set; }

    // Whether merging into the default branch needs the ShipMate Scope Check. False on a repo where GitHub gave no way to
    // require it (e.g. a personal repo without branch protection), so the UI can warn.
    public bool GitHubMergeGateEnabled { get; set; }

    // The newest commit of the default branch already checked for code that bypassed ShipMate; the next check starts after it.
    public string? LastAuditedMainSha { get; set; }

    public decimal DriftConfidenceThreshold { get; set; } = 0.75m;

    // Set when ShipMate loses control of the repo (its App was uninstalled): the branch protections no longer
    // hold, so submits are refused and SHIP is blocked until the App is installed again.
    public DateTime? GovernanceSuspendedAt { get; set; }
    public string? GovernanceSuspendedReason { get; set; }

    public const string AppUninstalledReason = "APP_UNINSTALLED";

    // Set exactly once, by LOCK, on this workspace's first-ever Lock. SHIP uses it to compute
    // Time-to-Ship — must never be overwritten by later Locks/Feature Challenges.
    public DateTime? FirstLockedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
