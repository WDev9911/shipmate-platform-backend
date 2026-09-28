using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Constants;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class UpdateCommittedFeatureRequestValidator : AbstractValidator<UpdateCommittedFeatureRequest>
{
    public UpdateCommittedFeatureRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FeatureConstraints.NameMaxLength).When(x => x.Name is not null);
        RuleFor(x => x.Description).NotEmpty().When(x => x.Description is not null);
    }
}
