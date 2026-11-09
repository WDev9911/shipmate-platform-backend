using FluentValidation;
using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Validators.Build;

public class ApproveBuildRequestValidator : AbstractValidator<ApproveBuildRequest>
{
    public ApproveBuildRequestValidator()
    {
        RuleFor(x => x.CommitSha).Matches("^[0-9a-fA-F]{40}$")
            .When(x => x.CommitSha is not null)
            .WithMessage("'Commit Sha' must be a full 40-character commit hash.");
    }
}
