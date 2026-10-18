using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Validators.Workspaces;

public class UpdateMySkillRequestValidator : AbstractValidator<UpdateMySkillRequest>
{
    public UpdateMySkillRequestValidator()
    {
        RuleFor(x => x.SkillLevel).InclusiveBetween(1, 5);
    }
}
