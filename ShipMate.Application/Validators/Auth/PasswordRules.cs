using FluentValidation;

namespace ShipMate.Application.Validators.Auth;

/// <summary>What a new password must look like, in one place: registration, reset and the seeded administrator use it.</summary>
public static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> MeetsPasswordRules<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least 1 uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least 1 digit.");

    // Internal, so the assembly scan doesn't register a validator for every string a controller takes.
    private sealed class PasswordOnlyValidator : AbstractValidator<string>
    {
        public PasswordOnlyValidator() => RuleFor(password => password).MeetsPasswordRules();
    }

    private static readonly PasswordOnlyValidator Validator = new();

    public static bool IsValid(string? password) => Validator.Validate(password ?? string.Empty).IsValid;
}
