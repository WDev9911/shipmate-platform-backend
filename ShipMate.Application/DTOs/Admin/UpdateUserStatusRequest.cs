using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

public class UpdateUserStatusRequest
{
    public UserStatus Status { get; set; }
}
