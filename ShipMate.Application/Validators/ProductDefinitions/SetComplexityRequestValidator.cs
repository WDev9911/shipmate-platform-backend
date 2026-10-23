using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Constants;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class SetComplexityRequestValidator : AbstractValidator<SetComplexityRequest>
{
    public SetComplexityRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.FeatureId).Distinct().Count() == items.Count)
            .WithMessage("Each feature may appear only once.");
        RuleForEach(x => x.Items)
            .ChildRules(item => item.RuleFor(i => i.Complexity).InclusiveBetween(ComplexityScale.Min, ComplexityScale.Max));
    }
}
