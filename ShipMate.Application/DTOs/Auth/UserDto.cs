using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsEmailVerified { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public string? GitHubUsername { get; set; }
    public NotificationPreference NotificationPreference { get; set; }
}
