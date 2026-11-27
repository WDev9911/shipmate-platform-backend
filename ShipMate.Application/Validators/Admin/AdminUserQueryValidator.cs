using FluentValidation;
using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Validators.Admin;

public class AdminUserQueryValidator : AbstractValidator<AdminUserQuery>
{
    public AdminUserQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(100);

        // The API also takes numbers for enums; this refuses the ones that name nothing, such as 1 (the old Manager).
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}
