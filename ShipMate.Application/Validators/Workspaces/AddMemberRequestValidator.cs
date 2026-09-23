using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Validators.Workspaces;

public class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}
