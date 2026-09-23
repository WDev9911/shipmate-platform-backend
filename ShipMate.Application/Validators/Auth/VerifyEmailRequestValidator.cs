using FluentValidation;
using ShipMate.Application.DTOs.Auth;

namespace ShipMate.Application.Validators.Auth;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Code)
            .NotEmpty()
            .Matches("^[0-9]{6}$").WithMessage("Code must be exactly 6 digits.");
    }
}
