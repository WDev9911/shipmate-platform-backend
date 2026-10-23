using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class UpdateProjectConstraintsRequestValidator : AbstractValidator<UpdateProjectConstraintsRequest>
{
    public UpdateProjectConstraintsRequestValidator()
    {
        RuleFor(x => x.Customer).MaximumLength(5000);
        RuleFor(x => x.Company).MaximumLength(5000);
        RuleFor(x => x.Team).MaximumLength(5000);
    }
}
