using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Validators.Workspaces;

public class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.VisionPrompt).NotEmpty().MaximumLength(5000);
    }
}
