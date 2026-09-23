using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Validators.Workspaces;

public class UpdateMemberRoleRequestValidator : AbstractValidator<UpdateMemberRoleRequest>
{
    public UpdateMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
