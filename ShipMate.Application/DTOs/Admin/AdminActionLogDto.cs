using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

public class AdminActionLogDto
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = string.Empty;
    public Guid? TargetUserId { get; set; }
    public string? TargetEmail { get; set; }
    public AdminAction Action { get; set; }
    public string? Subject { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
