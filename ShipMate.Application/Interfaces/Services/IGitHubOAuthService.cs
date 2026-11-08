using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Interfaces.Services;

public interface IGitHubOAuthService
{
    Task<GitHubTokenResult> ExchangeCodeAsync(string code);

    /// <summary>
    /// Trades a refresh token for a new access token. GitHub hands out a new refresh token too, and the old one stops
    /// working. Throws InvalidOrExpiredTokenException when GitHub refuses it (revoked, expired or already used).
    /// </summary>
    Task<GitHubTokenResult> RefreshTokenAsync(string refreshToken);
    Task<GitHubProfileResult> GetUserProfileAsync(string accessToken);
    Task<List<GitHubRepoDto>> GetUserRepositoriesAsync(string accessToken);
}
