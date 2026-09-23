using FluentValidation;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Validators.Workspaces;

public class LinkGitHubRepoRequestValidator : AbstractValidator<LinkGitHubRepoRequest>
{
    public LinkGitHubRepoRequestValidator()
    {
        RuleFor(x => x.Owner).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}
