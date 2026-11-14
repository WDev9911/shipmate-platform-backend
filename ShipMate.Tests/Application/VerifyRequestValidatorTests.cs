using ShipMate.Application.DTOs.Verify;
using ShipMate.Application.Validators.Verify;

namespace ShipMate.Tests.Application;

public class VerifyRequestValidatorTests
{
    private readonly SetVerificationItemApplicabilityRequestValidator _applicability = new();
    private readonly CheckVerificationItemRequestValidator _check = new();

    [Fact]
    public void NotApplicable_WithoutAReason_IsInvalid()
    {
        var result = _applicability.Validate(new SetVerificationItemApplicabilityRequest { Applicable = false });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NotApplicable_WithAShortReason_IsInvalid()
    {
        var result = _applicability.Validate(new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = "no" });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void NotApplicable_WithAReasonOfTenCharacters_IsValid()
    {
        var result = _applicability.Validate(
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = new string('x', 10) });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void NotApplicable_WithAReasonLongerThan500_IsInvalid()
    {
        var result = _applicability.Validate(
            new SetVerificationItemApplicabilityRequest { Applicable = false, Reason = new string('x', 501) });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Applicable_WithoutAReason_IsValid()
    {
        var result = _applicability.Validate(new SetVerificationItemApplicabilityRequest { Applicable = true });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Note_OfExactly500_IsValid_AndLongerIsNot()
    {
        Assert.True(_check.Validate(new CheckVerificationItemRequest { Checked = true, Note = new string('x', 500) }).IsValid);
        Assert.False(_check.Validate(new CheckVerificationItemRequest { Checked = true, Note = new string('x', 501) }).IsValid);
    }

    [Fact]
    public void NoNote_IsValid()
    {
        Assert.True(_check.Validate(new CheckVerificationItemRequest { Checked = false }).IsValid);
    }
}
