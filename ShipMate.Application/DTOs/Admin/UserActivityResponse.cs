using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

public class UserActivityResponse
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;

    public List<LoginSessionDto> LoginHistory { get; set; } = [];
    public List<AdminActionDto> AdminActions { get; set; } = [];
}

public class LoginSessionDto
{
    public DateTime CreatedAt { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class AdminActionDto
{
    public Guid AdminUserId { get; set; }
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminDisplayName { get; set; } = string.Empty;
    public AdminAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
