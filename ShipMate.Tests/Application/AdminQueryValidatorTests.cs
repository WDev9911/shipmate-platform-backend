using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Validators.Admin;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class AdminQueryValidatorTests
{
    private readonly AdminUserQueryValidator _users = new();
    private readonly AdminActionLogQueryValidator _logs = new();

    [Fact]
    public void Users_DefaultsAndTypicalFilters_AreValid()
    {
        Assert.True(_users.Validate(new AdminUserQuery()).IsValid);
        Assert.True(_users.Validate(new AdminUserQuery
        {
            Page = 5, PageSize = 100, Search = "dev", Role = UserRole.Administrator, Status = UserStatus.Locked
        }).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(1, 1000000)]
    public void Users_PageAndPageSizeOutOfRange_AreInvalid(int page, int pageSize)
    {
        Assert.False(_users.Validate(new AdminUserQuery { Page = page, PageSize = pageSize }).IsValid);
    }

    [Fact]
    public void Users_SearchOverTheLimit_IsInvalid()
    {
        Assert.True(_users.Validate(new AdminUserQuery { Search = new string('a', 100) }).IsValid);
        Assert.False(_users.Validate(new AdminUserQuery { Search = new string('a', 101) }).IsValid);
    }

    // 1 was the account-level Manager, removed from the enum: it must not be taken for something else.
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void Users_NumbersThatNameNoRoleOrStatus_AreInvalid(int number)
    {
        Assert.False(_users.Validate(new AdminUserQuery { Role = (UserRole)number }).IsValid);
        Assert.False(_users.Validate(new AdminUserQuery { Status = (UserStatus)(number + 10) }).IsValid);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 101)]
    public void ActionLogs_PageAndPageSizeOutOfRange_AreInvalid(int page, int pageSize)
    {
        Assert.False(_logs.Validate(new AdminActionLogQuery { Page = page, PageSize = pageSize }).IsValid);
    }

    [Fact]
    public void ActionLogs_AnUnknownAction_IsInvalid_AndAKnownOneIsValid()
    {
        Assert.True(_logs.Validate(new AdminActionLogQuery { Action = AdminAction.StatusChanged }).IsValid);
        Assert.False(_logs.Validate(new AdminActionLogQuery { Action = (AdminAction)99 }).IsValid);
    }
}
