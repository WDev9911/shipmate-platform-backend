using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class AddFeatureDependencyRequestValidator : AbstractValidator<AddFeatureDependencyRequest>
{
    public AddFeatureDependencyRequestValidator()
    {
        RuleFor(x => x.DependsOnFeatureId).NotEmpty();
    }
}
