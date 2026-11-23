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
    private readonly IWorkspaceNotifier _notifier;
    private readonly IEmailSender _emailSender;
    private readonly IMapper _mapper;

    public AdminService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAdminActionLogRepository adminActionLogRepository,
        IWorkspaceNotifier notifier,
        IEmailSender emailSender,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _adminActionLogRepository = adminActionLogRepository;
        _notifier = notifier;
        _emailSender = emailSender;
        _mapper = mapper;
    }

    public async Task<PagedResult<AdminUserListItemDto>> GetUsersAsync(AdminUserQuery query)
    {
        var (users, totalCount) = await _userRepository.GetPagedAsync(query);

        return new PagedResult<AdminUserListItemDto>
        {
            Items = _mapper.Map<List<AdminUserListItemDto>>(users),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
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

        // Administrators are not managed from here: two of them locking each other at the same moment would leave
        // nobody to undo it, and a count can't prevent that. A new administrator comes from AdminSeed, not from a role change.
        if (user.Role == UserRole.Administrator)
        {
            throw new AdminAccountProtectedException();
        }

        // A removed account is gone for good, so this comes before "no change".
        if (user.Status == UserStatus.Removed)
        {
            throw new AccountRemovedFinalException();
        }

        if (user.Status == newStatus)
        {
            throw new NoChangeException();
        }

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

        if (newStatus != UserStatus.Active)
        {
            // After the save, so a connection can't be cut for a lock that then failed to be stored.
            await _notifier.DisconnectUserAsync(targetUserId);
            await NotifyAccountClosedAsync(user, newStatus);
        }

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

    public async Task<PagedResult<AdminActionLogDto>> GetActionLogsAsync(AdminActionLogQuery query)
    {
        var (logs, totalCount) = await _adminActionLogRepository.GetPagedAsync(query);

        return new PagedResult<AdminActionLogDto>
        {
            Items = logs.Select(l => new AdminActionLogDto
            {
                Id = l.Id,
                AdminUserId = l.AdminUserId,
                AdminEmail = l.AdminUser.Email,
                AdminDisplayName = l.AdminUser.DisplayName,
                TargetUserId = l.TargetUserId,
                TargetEmail = l.TargetUser?.Email,
                Action = l.Action,
                Subject = l.Subject,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                CreatedAt = l.CreatedAt
            }).ToList(),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    // A person who can't sign in can't read a message in the app, so this goes by email whatever their preference is.
    // A mail that fails to send never undoes the lock.
    private Task NotifyAccountClosedAsync(User user, UserStatus newStatus)
    {
        var what = newStatus == UserStatus.Removed ? "removed" : "locked";
        return _emailSender.TrySendAsync(
            user.Email,
            $"Your ShipMate account has been {what}",
            $"""
             <p>Hi {user.DisplayName},</p>
             <p>Your ShipMate account was just {what} by an administrator, so you can no longer sign in.</p>
             <p>If you think this is a mistake, please contact the platform administrator.</p>
             """);
    }
}
