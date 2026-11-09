using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Validators.Build;

namespace ShipMate.Tests.Application;

public class OverrideDriftAlertRequestValidatorTests
{
    private readonly OverrideDriftAlertRequestValidator _validator = new();

    [Fact]
    public void AReason_Passes() =>
        Assert.True(_validator.Validate(new OverrideDriftAlertRequest { Reason = "Client asked for it" }).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NoReason_IsRejected(string reason) =>
        Assert.False(_validator.Validate(new OverrideDriftAlertRequest { Reason = reason }).IsValid);

    [Fact]
    public void ATooLongReason_IsRejected() =>
        Assert.False(_validator.Validate(new OverrideDriftAlertRequest { Reason = new string('a', 1001) }).IsValid);
}
