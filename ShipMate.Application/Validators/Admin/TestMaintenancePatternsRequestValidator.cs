using FluentValidation;
using Microsoft.Extensions.Configuration;
using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Validators.Admin;

public class TestMaintenancePatternsRequestValidator : AbstractValidator<TestMaintenancePatternsRequest>
{
    public const int MaxMessages = 100;
    public const int MaxFiles = 100;
    private const int MaxMessageLength = 1000;

    // The same limit BUILD puts on the diff it sends to the AI; without one a request could make the server read tens of MB.
    private const int DefaultMaxDiffCharacters = 60_000;

    public TestMaintenancePatternsRequestValidator(IConfiguration configuration)
    {
        var maxPatchCharacters = int.TryParse(configuration["Build:MaxDiffCharacters"], out var configured) && configured > 0
            ? configured
            : DefaultMaxDiffCharacters;

        RuleFor(x => x.Patterns!).MeetsMaintenancePatternRules().When(x => x.Patterns is not null);

        RuleFor(x => x.CommitMessages)
            .NotNull()
            .Must(messages => messages.Count is >= 1 and <= MaxMessages)
            .WithMessage($"Give between 1 and {MaxMessages} commit messages.")
            .Must(messages => messages.All(m => !string.IsNullOrWhiteSpace(m) && m.Length <= MaxMessageLength))
            .WithMessage($"Each commit message must be 1 to {MaxMessageLength} characters.");

        RuleFor(x => x.Files!)
            .Must(files => files.Count <= MaxFiles)
            .WithMessage($"Give at most {MaxFiles} files.")
            .Must(files => files.All(f => !string.IsNullOrWhiteSpace(f.Path)))
            .WithMessage("Every file needs a path.")
            .Must(files => files.Sum(f => f.Patch?.Length ?? 0) <= maxPatchCharacters)
            .WithMessage($"The patches together may have at most {maxPatchCharacters} characters.")
            .When(x => x.Files is not null);
    }
}
