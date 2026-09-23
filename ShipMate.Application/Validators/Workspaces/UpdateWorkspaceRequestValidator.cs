using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Validators.Workspaces;

public class UpdateWorkspaceRequestValidator : AbstractValidator<UpdateWorkspaceRequest>
{
    public UpdateWorkspaceRequestValidator()
    {
        // Only validate a field when the caller actually sent it — omitted (null) is valid
        // and means "leave unchanged".
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name is not null);
        RuleFor(x => x.VisionPrompt).NotEmpty().MaximumLength(5000).When(x => x.VisionPrompt is not null);
    }
}
