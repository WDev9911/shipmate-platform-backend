using FluentValidation;
using ShipMate.Application.DTOs.Verify;

namespace ShipMate.Application.Validators.Verify;

public class CheckVerificationItemRequestValidator : AbstractValidator<CheckVerificationItemRequest>
{
    public CheckVerificationItemRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class SetVerificationItemApplicabilityRequestValidator : AbstractValidator<SetVerificationItemApplicabilityRequest>
{
    public SetVerificationItemApplicabilityRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(500)
            .When(x => !x.Applicable)
            .WithMessage("Give a reason (10 to 500 characters) for skipping this check.");
    }
}
