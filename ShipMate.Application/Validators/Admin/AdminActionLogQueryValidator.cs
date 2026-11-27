using FluentValidation;
using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Validators.Admin;

public class AdminActionLogQueryValidator : AbstractValidator<AdminActionLogQuery>
{
    public AdminActionLogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Action).IsInEnum();
    }
}
