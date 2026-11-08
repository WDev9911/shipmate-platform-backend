using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Validators.Admin;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class UpdateUserRoleRequestValidatorTests
{
    private readonly UpdateUserRoleRequestValidator _validator = new();

    [Theory]
    [InlineData(UserRole.Developer)]
    [InlineData(UserRole.Administrator)]
    public void AnAccountRole_Passes(UserRole role) =>
        Assert.True(_validator.Validate(new UpdateUserRoleRequest { Role = role }).IsValid);

    // 1 was Manager, which is now only a workspace role; it must be refused, not read as another role.
    [Fact]
    public void TheOldManagerValue_IsRejected() =>
        Assert.False(_validator.Validate(new UpdateUserRoleRequest { Role = (UserRole)1 }).IsValid);
}
