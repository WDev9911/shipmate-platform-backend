using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Workspaces;

public class WorkspaceDto
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VisionPrompt { get; set; } = string.Empty;
    public DateTime? LaunchDeadline { get; set; }
    public WorkspaceStatus Status { get; set; }

    public string? GitHubRepoOwner { get; set; }
    public string? GitHubRepoName { get; set; }

    // False means the repo can't be hard-locked on GitHub (personal repo): the FE should warn the Manager.
    public bool GitHubRepoIsOrganization { get; set; }

    // False when merging into the default branch can't be made to require the ShipMate check (shown as a warning).
    public bool GitHubMergeGateEnabled { get; set; }
    public decimal DriftConfidenceThreshold { get; set; }

    public DateTime? FirstLockedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
