using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Domain.Constants;

namespace ShipMate.Application.Validators.Workspaces;

public class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.SkillLevel).InclusiveBetween(ComplexityScale.Min, ComplexityScale.Max);
    }
}
