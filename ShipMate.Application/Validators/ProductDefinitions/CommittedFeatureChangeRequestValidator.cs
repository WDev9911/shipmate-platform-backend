using FluentValidation;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Validators.ProductDefinitions;

public class CommittedFeatureChangeRequestValidator : AbstractValidator<CommittedFeatureChangeRequest>
{
    public CommittedFeatureChangeRequestValidator()
    {
        RuleFor(x => x.Action)
            .Must(action => action is FeatureChangeAction.Edit or FeatureChangeAction.Exclude)
            .WithMessage("Action must be Edit or Exclude.");

        RuleFor(x => x.Reason).NotEmpty();

        RuleFor(x => x.CustomerNotifiedConfirmed)
            .Equal(true)
            .WithMessage("You must confirm that the customer has been or will be informed of this change.");

        When(x => x.Action == FeatureChangeAction.Edit, () =>
        {
            RuleFor(x => x)
                .Must(x => x.Name is not null || x.Description is not null || x.Scope is not null)
                .WithName("Content")
                .WithMessage("An edit must change at least one of name, description or scope.");

            RuleFor(x => x.Name).NotEmpty().MaximumLength(FeatureConstraints.NameMaxLength).When(x => x.Name is not null);
            RuleFor(x => x.Description).NotEmpty().When(x => x.Description is not null);
        });

        RuleFor(x => x.DependentsResolution).IsInEnum().When(x => x.DependentsResolution is not null);
    }
}
