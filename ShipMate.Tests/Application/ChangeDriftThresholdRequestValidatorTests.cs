using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Validators.Build;

namespace ShipMate.Tests.Application;

public class ChangeDriftThresholdRequestValidatorTests
{
    private readonly ChangeDriftThresholdRequestValidator _validator = new();

    [Theory]
    [InlineData(0.8)]
    [InlineData(0.75)]
    [InlineData(0.7)]
    public void UpToTwoDecimals_Passes(double value) =>
        Assert.True(_validator.Validate(new ChangeDriftThresholdRequest { Value = (decimal)value }).IsValid);

    [Theory]
    [InlineData(0.755)]
    [InlineData(0.8001)]
    public void MoreThanTwoDecimals_IsRejected(double value) =>
        Assert.False(_validator.Validate(new ChangeDriftThresholdRequest { Value = (decimal)value }).IsValid);
}
