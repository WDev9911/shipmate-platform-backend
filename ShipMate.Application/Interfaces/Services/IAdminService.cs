using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Interfaces.Services;

public interface IAdminService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(int page, int pageSize);
    Task<UserDto> UpdateUserStatusAsync(Guid adminUserId, Guid targetUserId, UserStatus newStatus);
    Task<UserDto> UpdateUserRoleAsync(Guid adminUserId, Guid targetUserId, UserRole newRole);
    Task<UserActivityResponse> GetUserActivityAsync(Guid targetUserId);
}
