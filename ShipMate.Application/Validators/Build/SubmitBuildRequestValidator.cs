using FluentValidation;
using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Validators.Build;

public class SubmitBuildRequestValidator : AbstractValidator<SubmitBuildRequest>
{
    public SubmitBuildRequestValidator()
    {
        RuleFor(x => x.BranchName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.CommitSha).NotEmpty().Matches("^[0-9a-fA-F]{40}$")
            .WithMessage("'Commit Sha' must be a full 40-character commit hash.");
    }
}
