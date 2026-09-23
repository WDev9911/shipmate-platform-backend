using AutoMapper;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Common;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAdminActionLogRepository _adminActionLogRepository;
    private readonly IMapper _mapper;

    public AdminService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAdminActionLogRepository adminActionLogRepository,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _adminActionLogRepository = adminActionLogRepository;
        _mapper = mapper;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(int page, int pageSize)
    {
        var (users, totalCount) = await _userRepository.GetPagedAsync(page, pageSize);

        return new PagedResult<AdminUserListItemDto>
        {
            Items = _mapper.Map<List<AdminUserListItemDto>>(users),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserDto> UpdateUserStatusAsync(Guid adminUserId, Guid targetUserId, UserStatus newStatus)
    {
        if (adminUserId == targetUserId)
        {
            throw new SelfActionNotAllowedException();
        }

        var user = await _userRepository.GetByIdAsync(targetUserId)
            ?? throw new NotFoundException("User", targetUserId);

        var oldStatus = user.Status;
        user.Status = newStatus;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        await _adminActionLogRepository.AddAsync(new AdminActionLog
        {
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            Action = AdminAction.StatusChanged,
            OldValue = oldStatus.ToString(),
            NewValue = newStatus.ToString()
        });

        // Locking (or removing) an account must also kill any session still open on it right away —
        // otherwise a locked user keeps working until their access/refresh token naturally expires.
        if (newStatus != UserStatus.Active)
        {
            var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(targetUserId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
        }

        await _userRepository.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateUserRoleAsync(Guid adminUserId, Guid targetUserId, UserRole newRole)
    {
        if (adminUserId == targetUserId)
        {
            throw new SelfActionNotAllowedException();
        }

        var user = await _userRepository.GetByIdAsync(targetUserId)
            ?? throw new NotFoundException("User", targetUserId);

        var oldRole = user.Role;
        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        await _adminActionLogRepository.AddAsync(new AdminActionLog
        {
            AdminUserId = adminUserId,
            TargetUserId = targetUserId,
            Action = AdminAction.RoleChanged,
            OldValue = oldRole.ToString(),
            NewValue = newRole.ToString()
        });

        await _userRepository.SaveChangesAsync();

        return _mapper.Map<UserDto>(user);
    }

    public async Task<UserActivityResponse> GetUserActivityAsync(Guid targetUserId)
    {
        var targetUser = await _userRepository.GetByIdAsync(targetUserId)
            ?? throw new NotFoundException("User", targetUserId);

        var sessions = await _refreshTokenRepository.GetAllByUserIdAsync(targetUserId);
        var actionLogs = await _adminActionLogRepository.GetByTargetUserIdAsync(targetUserId);

        return new UserActivityResponse
        {
            UserId = targetUser.Id,
            UserEmail = targetUser.Email,
            UserDisplayName = targetUser.DisplayName,
            LoginHistory = sessions
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new LoginSessionDto
                {
                    CreatedAt = t.CreatedAt,
                    DeviceInfo = t.DeviceInfo,
                    ExpiresAt = t.ExpiresAt,
                    RevokedAt = t.RevokedAt
                })
                .ToList(),
            AdminActions = actionLogs
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AdminActionDto
                {
                    AdminUserId = a.AdminUserId,
                    AdminEmail = a.AdminUser.Email,
                    AdminDisplayName = a.AdminUser.DisplayName,
                    Action = a.Action,
                    OldValue = a.OldValue,
                    NewValue = a.NewValue,
                    CreatedAt = a.CreatedAt
                })
                .ToList()
        };
    }
}
