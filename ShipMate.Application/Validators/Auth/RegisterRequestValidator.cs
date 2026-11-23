using FluentValidation;
using ShipMate.Application.DTOs.Auth;

namespace ShipMate.Application.Validators.Auth;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password).MeetsPasswordRules();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("ConfirmPassword must match Password.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(100);
    }
}
