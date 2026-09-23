using FluentValidation;
using ShipMate.Application.DTOs.Auth;

namespace ShipMate.Application.Validators.Auth;

public class GitHubExchangeRequestValidator : AbstractValidator<GitHubExchangeRequest>
{
    public GitHubExchangeRequestValidator()
    {
        RuleFor(x => x.HandoffCode).NotEmpty();
    }
}
