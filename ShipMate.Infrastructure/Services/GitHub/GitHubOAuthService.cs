using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Infrastructure.Services.GitHub;

public class GitHubOAuthService : IGitHubOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GitHubOAuthService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<GitHubTokenResult> ExchangeCodeAsync(string code)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _configuration["GitHub:ClientId"]!,
                ["client_secret"] = _configuration["GitHub:ClientSecret"]!,
                ["code"] = code,
                ["redirect_uri"] = _configuration["GitHub:RedirectUri"]!
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GitHubAccessTokenPayload>()
            ?? throw new InvalidOrExpiredTokenException();

        if (payload.Error is not null || payload.AccessToken is null)
        {
            throw new InvalidOrExpiredTokenException();
        }

        var now = DateTime.UtcNow;
        return new GitHubTokenResult
        {
            AccessToken = payload.AccessToken,
            Scope = payload.Scope ?? string.Empty,
            ExpiresAt = payload.ExpiresIn.HasValue ? now.AddSeconds(payload.ExpiresIn.Value) : null,
            RefreshToken = payload.RefreshToken,
            RefreshTokenExpiresAt = payload.RefreshTokenExpiresIn.HasValue
                ? now.AddSeconds(payload.RefreshTokenExpiresIn.Value)
                : null
        };
    }

    public async Task<GitHubProfileResult> GetUserProfileAsync(string accessToken)
    {
        var profile = await SendGitHubApiRequestAsync<GitHubUserPayload>("https://api.github.com/user", accessToken)
            ?? throw new InvalidOperationException("Empty response from GitHub user endpoint.");

        var email = profile.Email;
        if (string.IsNullOrEmpty(email))
        {
            var emails = await SendGitHubApiRequestAsync<List<GitHubEmailPayload>>(
                "https://api.github.com/user/emails", accessToken) ?? [];
            email = emails.FirstOrDefault(e => e.Primary && e.Verified)?.Email;
        }

        return new GitHubProfileResult
        {
            GitHubId = profile.Id.ToString(),
            Username = profile.Login,
            Email = email,
            AvatarUrl = profile.AvatarUrl,
            Name = profile.Name
        };
    }

    public async Task<List<GitHubRepoDto>> GetUserRepositoriesAsync(string accessToken)
    {
        var repos = await SendGitHubApiRequestAsync<List<GitHubRepoPayload>>(
            "https://api.github.com/user/repos?per_page=100&sort=updated", accessToken) ?? [];

        return repos.Select(r => new GitHubRepoDto
        {
            Owner = r.Owner.Login,
            Name = r.Name,
            IsPrivate = r.Private,
            DefaultBranch = r.DefaultBranch
        }).ToList();
    }

    private async Task<T?> SendGitHubApiRequestAsync<T>(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("ShipMate");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>();
    }
}
