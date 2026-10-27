using FluentValidation;
using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Validators.Build;

public class OverrideDriftAlertRequestValidator : AbstractValidator<OverrideDriftAlertRequest>
{
    public OverrideDriftAlertRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
