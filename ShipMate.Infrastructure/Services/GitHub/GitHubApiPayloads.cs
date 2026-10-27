using System.Text.Json.Serialization;

namespace ShipMate.Infrastructure.Services.GitHub;

internal class GitHubAccessTokenPayload
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int? RefreshTokenExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

internal class GitHubUserPayload
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; set; }
}

internal class GitHubEmailPayload
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }
}

internal class GitHubRepoPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public GitHubRepoOwnerPayload Owner { get; set; } = new();
}

internal class GitHubRepoOwnerPayload
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;
}

// GET /repos/{owner}/{repo}/git/ref/heads/{branch}

internal class GitHubRefPayload

{

    [JsonPropertyName("object")]

    public GitHubRefObjectPayload Object { get; set; } = new();

}



internal class GitHubRefObjectPayload

{

    [JsonPropertyName("sha")]

    public string Sha { get; set; } = string.Empty;

}

// GET /repos/{owner}/{repo}/installation
internal class GitHubInstallationPayload
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("account")]
    public GitHubInstallationAccountPayload Account { get; set; } = new();
}

internal class GitHubInstallationAccountPayload
{
    // "Organization" or "User"
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

// GET /repos/{owner}/{repo}/compare/{base}...{head}
internal class GitHubComparePayload
{
    [JsonPropertyName("commits")]
    public List<GitHubCompareCommitPayload> Commits { get; set; } = new();

    [JsonPropertyName("files")]
    public List<GitHubCompareFilePayload> Files { get; set; } = new();
}

internal class GitHubCompareCommitPayload
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("commit")]
    public GitHubCommitDetailPayload Commit { get; set; } = new();
}

internal class GitHubCommitDetailPayload
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

internal class GitHubCompareFilePayload
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    // Missing for binary files and for diffs GitHub considers too large to show.
    [JsonPropertyName("patch")]
    public string? Patch { get; set; }
}

// POST /app/installations/{id}/access_tokens
internal class GitHubInstallationTokenPayload
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }
}

// GET /repos/{owner}/{repo}/branches/{branch}/protection (only what the merge gate needs)
internal class GitHubBranchProtectionPayload
{
    // Null when the branch is protected but requires no status checks.
    [JsonPropertyName("required_status_checks")]
    public GitHubRequiredStatusChecksPayload? RequiredStatusChecks { get; set; }
}

internal class GitHubRequiredStatusChecksPayload
{
    [JsonPropertyName("strict")]
    public bool Strict { get; set; }

    [JsonPropertyName("checks")]
    public List<GitHubRequiredCheckPayload> Checks { get; set; } = new();
}

internal class GitHubRequiredCheckPayload
{
    [JsonPropertyName("context")]
    public string Context { get; set; } = string.Empty;

    // Null when any app may provide the check.
    [JsonPropertyName("app_id")]
    public long? AppId { get; set; }
}

// GET /repos/{owner}/{repo}/commits/{sha}/pulls
internal class GitHubCommitPullPayload
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("merged_at")]
    public DateTimeOffset? MergedAt { get; set; }

    [JsonPropertyName("head")]
    public GitHubRefObjectPayload Head { get; set; } = new();
}

// GET /repos/{owner}/{repo}/commits/{sha}/check-runs
internal class GitHubCheckRunsPayload
{
    [JsonPropertyName("check_runs")]
    public List<GitHubCheckRunPayload> CheckRuns { get; set; } = new();
}

internal class GitHubCheckRunPayload
{
    [JsonPropertyName("conclusion")]
    public string? Conclusion { get; set; }
}
