using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Interfaces.Services;

public interface IGitHubOAuthService
{
    Task<GitHubTokenResult> ExchangeCodeAsync(string code);
    Task<GitHubProfileResult> GetUserProfileAsync(string accessToken);
    Task<List<GitHubRepoDto>> GetUserRepositoriesAsync(string accessToken);
}
