using FluentValidation;
using ShipMate.Application.DTOs.Users;

namespace ShipMate.Application.Validators.Users;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100).When(x => x.DisplayName is not null);
        RuleFor(x => x.NotificationPreference).IsInEnum().When(x => x.NotificationPreference is not null);
    }
}
