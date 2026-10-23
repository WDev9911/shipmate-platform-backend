using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

public class WorkspaceMember : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public WorkspaceMemberRole Role { get; set; } = WorkspaceMemberRole.Developer;
    public WorkspaceMemberStatus Status { get; set; } = WorkspaceMemberStatus.Active;

    // Self-declared 1-5 skill level; null until the member fills it in. LOCK uses it for the Skill Ceiling.
    public int? SkillLevel { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
