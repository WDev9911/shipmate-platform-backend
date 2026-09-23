using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Users;

public class UpdateProfileRequest
{
    // True partial update: a field left null means "don't change it".
    public string? DisplayName { get; set; }
    public NotificationPreference? NotificationPreference { get; set; }
}
