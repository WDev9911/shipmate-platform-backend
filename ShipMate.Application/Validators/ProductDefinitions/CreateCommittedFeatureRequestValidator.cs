using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Constants;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class CreateCommittedFeatureRequestValidator : AbstractValidator<CreateCommittedFeatureRequest>
{
    public CreateCommittedFeatureRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FeatureConstraints.NameMaxLength);
        RuleFor(x => x.Description).NotEmpty();
    }
}
