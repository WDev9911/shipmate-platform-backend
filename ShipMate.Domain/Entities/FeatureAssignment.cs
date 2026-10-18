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

    // Only the name is stored; creating the branch on GitHub is BUILD's infrastructure.
    public string BranchName { get; set; } = string.Empty;
}
