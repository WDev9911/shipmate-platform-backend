using FluentValidation;
using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Validators.Admin;

public class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
    }
}
