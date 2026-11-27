using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// Locking and removing accounts, the last administrator, the user list and the log of what administrators did.
public class AdminServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAdminActionLogRepository _logs = Substitute.For<IAdminActionLogRepository>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly IEmailSender _emails = Substitute.For<IEmailSender>();
    private readonly IMapper _mapper = Substitute.For<IMapper>();
    private readonly AdminService _service;

    private readonly Guid _admin = Guid.NewGuid();

    public AdminServiceTests()
    {
        _mapper.Map<UserDto>(Arg.Any<object>()).Returns(call => new UserDto { Id = ((User)call.Arg<object>()).Id });
        _refreshTokens.GetActiveTokensByUserIdAsync(Arg.Any<Guid>()).Returns([]);
        _service = new AdminService(_users, _refreshTokens, _logs, _notifier, _emails, _mapper);
    }

    private User Account(UserRole role = UserRole.Developer, UserStatus status = UserStatus.Active)
    {
        var user = new User { Email = "someone@example.com", DisplayName = "Someone", Role = role, Status = status };
        _users.GetByIdAsync(user.Id).Returns(user);
        return user;
    }

    // ---------- Changing the status of an account ----------

    [Fact]
    public async Task Lock_ClosesTheAccountEverywhere_AndRecordsIt()
    {
        var user = Account();
        var session = new RefreshToken { UserId = user.Id };
        _refreshTokens.GetActiveTokensByUserIdAsync(user.Id).Returns([session]);

        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Locked);

        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.NotNull(session.RevokedAt);
        await _logs.Received(1).AddAsync(Arg.Is<AdminActionLog>(l =>
            l.AdminUserId == _admin && l.TargetUserId == user.Id && l.Action == AdminAction.StatusChanged
            && l.OldValue == "Active" && l.NewValue == "Locked"));
        await _notifier.Received(1).DisconnectUserAsync(user.Id);
        await _emails.Received(1).TrySendAsync(user.Email, Arg.Is<string>(s => s.Contains("locked")), Arg.Any<string>());
    }

    [Fact]
    public async Task Lock_CutsTheConnectionsOnlyAfterTheLockIsSaved()
    {
        var user = Account();

        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Locked);

        Received.InOrder(() =>
        {
            _users.SaveChangesAsync();
            _notifier.DisconnectUserAsync(user.Id);
        });
    }

    [Fact]
    public async Task Remove_SaysSoInTheEmail()
    {
        var user = Account();

        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Removed);

        Assert.Equal(UserStatus.Removed, user.Status);
        await _emails.Received(1).TrySendAsync(user.Email, Arg.Is<string>(s => s.Contains("removed")), Arg.Any<string>());
    }

    [Fact]
    public async Task Lock_WhenTheEmailFails_TheLockStays()
    {
        var user = Account();
        _emails.TrySendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Locked);

        Assert.Equal(UserStatus.Locked, user.Status);
        await _users.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Unlock_ReopensTheAccount_WithoutCuttingOrMailingAnyone()
    {
        var user = Account(status: UserStatus.Locked);

        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Active);

        Assert.Equal(UserStatus.Active, user.Status);
        await _logs.Received(1).AddAsync(Arg.Is<AdminActionLog>(l => l.OldValue == "Locked" && l.NewValue == "Active"));
        await _refreshTokens.DidNotReceive().GetActiveTokensByUserIdAsync(Arg.Any<Guid>());
        await _notifier.DidNotReceive().DisconnectUserAsync(Arg.Any<Guid>());
        await _emails.DidNotReceive().TrySendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Status_OfYourOwnAccount_IsRefused()
    {
        await Assert.ThrowsAsync<SelfActionNotAllowedException>(() =>
            _service.UpdateUserStatusAsync(_admin, _admin, UserStatus.Locked));
    }

    [Fact]
    public async Task Status_OfAnUnknownAccount_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateUserStatusAsync(_admin, Guid.NewGuid(), UserStatus.Locked));
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Locked)]
    public async Task Status_AlreadyThatValue_IsNoChange_AndNothingIsWritten(UserStatus status)
    {
        var user = Account(status: status);

        await Assert.ThrowsAsync<NoChangeException>(() => _service.UpdateUserStatusAsync(_admin, user.Id, status));

        await _logs.DidNotReceive().AddAsync(Arg.Any<AdminActionLog>());
        await _users.DidNotReceive().SaveChangesAsync();
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Removed)]
    public async Task Removed_IsFinal(UserStatus newStatus)
    {
        var user = Account(status: UserStatus.Removed);

        await Assert.ThrowsAsync<AccountRemovedFinalException>(() =>
            _service.UpdateUserStatusAsync(_admin, user.Id, newStatus));

        Assert.Equal(UserStatus.Removed, user.Status);
        await _logs.DidNotReceive().AddAsync(Arg.Any<AdminActionLog>());
    }

    // ---------- Administrators are not managed from here ----------

    [Theory]
    [InlineData(UserStatus.Active, UserStatus.Locked)]
    [InlineData(UserStatus.Active, UserStatus.Removed)]
    [InlineData(UserStatus.Locked, UserStatus.Active)]
    [InlineData(UserStatus.Locked, UserStatus.Removed)]
    public async Task AnAdministratorAccount_CannotBeLockedUnlockedOrRemoved_ByAnotherAdministrator(
        UserStatus current, UserStatus requested)
    {
        var other = Account(UserRole.Administrator, current);

        var error = await Assert.ThrowsAsync<AdminAccountProtectedException>(() =>
            _service.UpdateUserStatusAsync(_admin, other.Id, requested));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal("ADMIN_ACCOUNT_PROTECTED", error.ErrorCode);
        Assert.Equal(current, other.Status);
        await _logs.DidNotReceive().AddAsync(Arg.Any<AdminActionLog>());
        await _users.DidNotReceive().SaveChangesAsync();
        await _notifier.DidNotReceive().DisconnectUserAsync(Arg.Any<Guid>());
        await _emails.DidNotReceive().TrySendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ADeveloper_StillCanBeLockedAndUnlocked()
    {
        var user = Account();

        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Locked);
        await _service.UpdateUserStatusAsync(_admin, user.Id, UserStatus.Active);

        Assert.Equal(UserStatus.Active, user.Status);
        await _logs.Received(2).AddAsync(Arg.Any<AdminActionLog>());
    }

    // ---------- The user list and the log of administrator actions ----------

    [Fact]
    public async Task Users_AreListedWithTheRequestedPage()
    {
        var query = new AdminUserQuery { Page = 3, PageSize = 10, Search = "dev" };
        var users = new List<User> { new() { Email = "a@example.com" } };
        _users.GetPagedAsync(query).Returns((users, 25));
        _mapper.Map<List<AdminUserListItemDto>>(users).Returns([new AdminUserListItemDto { Email = "a@example.com" }]);

        var result = await _service.GetUsersAsync(query);

        Assert.Equal(25, result.TotalCount);
        Assert.Equal(3, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal("a@example.com", Assert.Single(result.Items).Email);
    }

    [Fact]
    public async Task ActionLogs_ShowTheAdministratorAndAnAccountlessChange()
    {
        var admin = new User { Email = "admin@example.com", DisplayName = "Admin" };
        var target = new User { Email = "dev@example.com" };
        var onAccount = new AdminActionLog
        {
            AdminUserId = admin.Id, AdminUser = admin, TargetUserId = target.Id, TargetUser = target,
            Action = AdminAction.StatusChanged, OldValue = "Active", NewValue = "Locked"
        };
        var onSettings = new AdminActionLog
        {
            AdminUserId = admin.Id, AdminUser = admin, Action = AdminAction.StatusChanged, Subject = "setting"
        };
        var query = new AdminActionLogQuery { Page = 2, PageSize = 5 };
        _logs.GetPagedAsync(query).Returns(([onAccount, onSettings], 7));

        var result = await _service.GetActionLogsAsync(query);

        Assert.Equal(7, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal("dev@example.com", result.Items[0].TargetEmail);
        Assert.Equal("admin@example.com", result.Items[0].AdminEmail);
        Assert.Null(result.Items[1].TargetUserId);
        Assert.Null(result.Items[1].TargetEmail);
        Assert.Equal("setting", result.Items[1].Subject);
    }
}
