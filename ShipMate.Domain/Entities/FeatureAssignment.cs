using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

/// <summary>The developer who took a locked feature, and the Git branch registered for them.</summary>
public class FeatureAssignment : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    // A feature has at most one assignment, enforced by a unique index so two simultaneous claims can't both win.
    public Guid FeatureId { get; set; }

    public Guid AssignedUserId { get; set; }
    public User AssignedUser { get; set; } = null!;

    public string BranchName { get; set; } = string.Empty;

    // When the branch was created on GitHub; null while the workspace has no linked repository or GitHub refused.
    public DateTime? BranchCreatedAt { get; set; }

    // Set when a Feature Challenge replaces the feature: the developer no longer holds it. The row stays as history.
    public DateTime? ReleasedAt { get; set; }

    // The commit the Bot last moved the branch to. A branch head that differs from it means someone pushed around the Bot.
    public string? LastApprovedSha { get; set; }

    // The branch head already reported as a direct push, so the same head is not reported on every later submit.
    public string? FlaggedHeadSha { get; set; }
}
