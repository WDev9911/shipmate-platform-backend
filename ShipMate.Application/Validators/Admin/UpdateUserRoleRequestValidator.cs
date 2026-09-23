using FluentValidation;
using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Validators.Admin;

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
