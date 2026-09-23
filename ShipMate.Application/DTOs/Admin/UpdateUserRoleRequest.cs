using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

public class UpdateUserRoleRequest
{
    public UserRole Role { get; set; }
}
