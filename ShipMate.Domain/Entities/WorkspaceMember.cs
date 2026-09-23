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

    public DateTime? UpdatedAt { get; set; }
}
