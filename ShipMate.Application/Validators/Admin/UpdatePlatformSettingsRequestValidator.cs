using FluentValidation;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Validators.Admin;

public class UpdatePlatformSettingsRequestValidator : AbstractValidator<UpdatePlatformSettingsRequest>
{
    public UpdatePlatformSettingsRequestValidator()
    {
        RuleFor(x => x.ComplexityBudgetPerDeveloper)
            .InclusiveBetween(PlatformSettingsLimits.MinBudgetPerDeveloper, PlatformSettingsLimits.MaxBudgetPerDeveloper);

        // The column keeps two decimals: a third one is refused instead of being rounded away by the database.
        RuleFor(x => x.DefaultDriftThreshold)
            .InclusiveBetween(DriftThresholdPolicy.Min, DriftThresholdPolicy.Max)
            .PrecisionScale(4, 2, ignoreTrailingZeros: true);

        RuleFor(x => x.Revision).GreaterThanOrEqualTo(0);

        RuleFor(x => x.MaintenanceCommitPatterns).MeetsMaintenancePatternRules();
    }
}

public static class MaintenancePatternRules
{
    /// <summary>1 to 30 patterns of 3 to 50 characters, none twice once trimmed and put in lower case.</summary>
    public static IRuleBuilderOptions<T, List<string>> MeetsMaintenancePatternRules<T>(this IRuleBuilder<T, List<string>> rule) =>
        rule
            .NotNull()
            .Must(patterns => patterns.Count is >= PlatformSettingsLimits.MinPatterns and <= PlatformSettingsLimits.MaxPatterns)
            .WithMessage($"Give between {PlatformSettingsLimits.MinPatterns} and {PlatformSettingsLimits.MaxPatterns} patterns.")
            .Must(patterns => patterns.All(p => p is not null
                && p.Trim().Length >= PlatformSettingsLimits.MinPatternLength
                && p.Trim().Length <= PlatformSettingsLimits.MaxPatternLength))
            .WithMessage($"Each pattern must have {PlatformSettingsLimits.MinPatternLength} to {PlatformSettingsLimits.MaxPatternLength} characters.")
            .Must(patterns => patterns.Where(p => p is not null).Select(p => p.Trim().ToLowerInvariant()).Distinct().Count()
                == patterns.Count)
            .WithMessage("A pattern is listed twice (case and surrounding spaces don't count).");
}
