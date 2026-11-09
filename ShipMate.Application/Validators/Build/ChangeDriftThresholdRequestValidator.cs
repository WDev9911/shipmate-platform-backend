using FluentValidation;
using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Validators.Build;

public class ChangeDriftThresholdRequestValidator : AbstractValidator<ChangeDriftThresholdRequest>
{
    public ChangeDriftThresholdRequestValidator()
    {
        // The column keeps two decimals; a value with more would be rounded into one the Manager never asked for.
        RuleFor(x => x.Value).PrecisionScale(4, 2, true)
            .WithMessage("'Value' can have at most 2 decimal places.");
    }
}
