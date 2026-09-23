using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Users;

namespace ShipMate.Application.Interfaces.Services;

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid userId);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}
