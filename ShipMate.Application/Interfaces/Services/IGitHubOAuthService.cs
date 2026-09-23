using ShipMate.Application.DTOs.Auth;

namespace ShipMate.Application.Interfaces.Services;

public interface IGitHubOAuthService
{
    Task<GitHubTokenResult> ExchangeCodeAsync(string code);
    Task<GitHubProfileResult> GetUserProfileAsync(string accessToken);
}
