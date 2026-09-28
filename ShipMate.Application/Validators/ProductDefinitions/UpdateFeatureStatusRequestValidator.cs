using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class UpdateFeatureStatusRequestValidator : AbstractValidator<UpdateFeatureStatusRequest>
{
    public UpdateFeatureStatusRequestValidator()
    {
        // pending_confirmation is only ever a default the system assigns; the developer resolves it.
        RuleFor(x => x.Status)
            .Must(status => status is FeatureStatus.Included or FeatureStatus.Excluded)
            .WithMessage("Status must be Included or Excluded.");

        RuleFor(x => x.DependentsResolution).IsInEnum().When(x => x.DependentsResolution is not null);
    }
}
