using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Workspaces;

public class UpdateMemberRoleRequest
{
    public WorkspaceMemberRole Role { get; set; }
}
