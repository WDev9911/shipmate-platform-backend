using FluentValidation;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Services;

namespace ShipMate.Application.Validators.Admin;

public class AiUsageQueryValidator : AbstractValidator<AiUsageQuery>
{
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(365);

    public AiUsageQueryValidator()
    {
        RuleFor(x => x)
            .Must(q =>
            {
                var (from, to) = AdminReportsService.Range(q, DateTime.UtcNow);
                return from < to;
            })
            .WithName("from")
            .WithMessage("'from' must be before 'to'.")
            .Must(q =>
            {
                var (from, to) = AdminReportsService.Range(q, DateTime.UtcNow);
                return to - from <= MaxRange;
            })
            .WithName("to")
            .WithMessage("The range can't be longer than 365 days.");
    }
}
