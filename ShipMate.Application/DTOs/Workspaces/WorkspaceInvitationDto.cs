using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Workspaces;

public class WorkspaceInvitationDto
{
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;
    public WorkspaceMemberRole Role { get; set; }
    public DateTime InvitedAt { get; set; }
}
