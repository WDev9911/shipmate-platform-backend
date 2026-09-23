using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Workspaces;

public class WorkspaceMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public WorkspaceMemberRole Role { get; set; }
    public WorkspaceMemberStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
