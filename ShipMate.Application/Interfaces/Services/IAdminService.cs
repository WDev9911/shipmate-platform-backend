using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Interfaces.Services;

public interface IAdminService
{
    Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQuery query);
    Task<UserDto> UpdateUserStatusAsync(Guid adminUserId, Guid targetUserId, UserStatus newStatus);
    Task<UserActivityResponse> GetUserActivityAsync(Guid targetUserId);
    Task<PagedResult<AdminActionLogDto>> GetActionLogsAsync(AdminActionLogQuery query);
}
