using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class SubmitChallengeRequestValidator : AbstractValidator<SubmitChallengeRequest>
{
    public SubmitChallengeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FeatureConstraints.NameMaxLength);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty();
    }
}

public class ConfirmChallengeComplexityRequestValidator : AbstractValidator<ConfirmChallengeComplexityRequest>
{
    public ConfirmChallengeComplexityRequestValidator()
    {
        RuleFor(x => x.Complexity).InclusiveBetween(ComplexityScale.Min, ComplexityScale.Max);
    }
}

public class ApproveChallengeRequestValidator : AbstractValidator<ApproveChallengeRequest>
{
    public ApproveChallengeRequestValidator()
    {
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.DependentsResolution).IsInEnum().When(x => x.DependentsResolution is not null);

        When(x => x.Action == ChallengeAction.RemoveAndReplace, () =>
        {
            RuleFor(x => x.RemoveFeatureId).NotNull().WithMessage("Choose the feature to remove.");
        });
    }
}

public class RejectChallengeRequestValidator : AbstractValidator<RejectChallengeRequest>
{
    public RejectChallengeRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}
