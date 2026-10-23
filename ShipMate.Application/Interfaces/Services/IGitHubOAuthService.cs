using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Interfaces.Services;

public interface IGitHubOAuthService
{
    Task<GitHubTokenResult> ExchangeCodeAsync(string code);
    Task<GitHubProfileResult> GetUserProfileAsync(string accessToken);
    Task<List<GitHubRepoDto>> GetUserRepositoriesAsync(string accessToken);

    /// <summary>
    /// Creates the branch from the head of the repository's default branch. Succeeds if the branch already exists;
    /// throws GitHubBranchCreationFailedException when GitHub refuses.
    /// </summary>
    Task CreateBranchFromDefaultAsync(string accessToken, string owner, string repo, string branchName);
}
