using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Infrastructure.Services.GitHub;

public class GitHubAppService : IGitHubAppService
{
    private const string ApiBase = "https://api.github.com";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public GitHubAppService(HttpClient httpClient, IConfiguration configuration, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
    }

    public string InstallUrl => $"https://github.com/apps/{_configuration["GitHubApp:Slug"]}/installations/new";

    public async Task<GitHubAppInstallation?> FindInstallationAsync(string owner, string repo)
    {
        var request = CreateRequest(HttpMethod.Get, $"{RepoUrl(owner, repo)}/installation", CreateAppJwt());
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<GitHubInstallationPayload>();

        return payload is null
            ? null
            : new GitHubAppInstallation(payload.Id, string.Equals(payload.Account.Type, "Organization", StringComparison.Ordinal));
    }

    public async Task<string?> GetBranchHeadShaAsync(long installationId, string owner, string repo, string branchName)
    {
        try
        {
            var url = $"{RepoUrl(owner, repo)}/git/ref/heads/{EscapePath(branchName)}";
            var token = await GetInstallationTokenAsync(installationId);
            var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, url, token));

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // The cached token no longer works (an uninstalled App takes its tokens with it): ask GitHub for a new one.
                // If the App is gone, that is where it says so, instead of this call failing once before it does.
                _cache.Remove(TokenCacheKey(installationId));
                token = await GetInstallationTokenAsync(installationId);
                response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, url, token));
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<GitHubRefPayload>();
            return payload?.Object.Sha;
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<string> GetDefaultBranchAsync(long installationId, string owner, string repo)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var repository = await GetAsync<GitHubRepoPayload>(RepoUrl(owner, repo), token);
            return repository.DefaultBranch;
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<GitHubComparison?> CompareAsync(
        long installationId, string owner, string repo, string baseRef, string headSha)
    {
        const int pageSize = 100;
        const int maxPages = 3;

        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var messages = new List<string>();
            var shas = new List<string>();
            var files = new List<ChangedFile>();
            var truncated = false;

            for (var page = 1; page <= maxPages; page++)
            {
                var url = $"{RepoUrl(owner, repo)}/compare/{EscapePath(baseRef)}...{headSha}?per_page={pageSize}&page={page}";
                var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, url, token));

                // 404 or 422: GitHub doesn't know the base or the head (a force push removed it).
                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
                {
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<GitHubComparePayload>()
                    ?? throw new HttpRequestException("Empty compare response from GitHub.");

                messages.AddRange(payload.Commits.Select(c => c.Commit.Message));
                shas.AddRange(payload.Commits.Select(c => c.Sha));
                files.AddRange(payload.Files.Select(f => new ChangedFile(f.Filename, f.Status, f.Patch, f.Changes)));

                if (payload.Commits.Count < pageSize && payload.Files.Count < pageSize)
                {
                    break;
                }

                // A full last page: there may be more that we won't read.
                truncated = page == maxPages;
            }

            return new GitHubComparison(messages, files, truncated, shas);
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task CreateProtectedBranchAsync(
        long installationId, bool isOrganization, string owner, string repo, string branchName)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var repoUrl = RepoUrl(owner, repo);

            var repository = await GetAsync<GitHubRepoPayload>(repoUrl, token);
            var head = await GetAsync<GitHubRefPayload>(
                $"{repoUrl}/git/ref/heads/{EscapePath(repository.DefaultBranch)}", token);

            // The lock is in place before the branch exists, so there is no moment when it is open.
            var lockedByRuleset = await EnsureFeatureLockRulesetAsync(repoUrl, token);

            var create = CreateRequest(HttpMethod.Post, $"{repoUrl}/git/refs", token);
            create.Content = JsonContent.Create(new { @ref = $"refs/heads/{branchName}", sha = head.Object.Sha });
            var created = await _httpClient.SendAsync(create);

            // 422 is GitHub's answer when the ref already exists: the branch is there, which is what we want.
            if (!created.IsSuccessStatusCode && created.StatusCode != HttpStatusCode.UnprocessableEntity)
            {
                throw new GitHubBranchCreationFailedException();
            }

            if (!lockedByRuleset)
            {
                await ProtectBranchAsync(repoUrl, token, isOrganization, branchName);
            }
        }
        catch (HttpRequestException)
        {
            throw new GitHubBranchCreationFailedException();
        }
    }

    public async Task<bool> FastForwardBranchAsync(
        long installationId, string owner, string repo, string branchName, string sha)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var request = CreateRequest(HttpMethod.Patch, $"{RepoUrl(owner, repo)}/git/refs/heads/{EscapePath(branchName)}", token);
            request.Content = JsonContent.Create(new { sha, force = false });
            var response = await _httpClient.SendAsync(request);

            // 422: not a fast-forward, or GitHub no longer has the commit.
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task DeleteBranchAsync(long installationId, string owner, string repo, string branchName)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var repoUrl = RepoUrl(owner, repo);
            var refUrl = $"{repoUrl}/git/refs/heads/{EscapePath(branchName)}";
            var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Delete, refUrl, token));

            // 422: a branch locked by an old per-branch protection rule (made before the ruleset) can't be deleted until
            // that rule is lifted. The ruleset lets the Bot through, so it never gets here.
            if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                var lifted = await _httpClient.SendAsync(
                    CreateRequest(HttpMethod.Delete, $"{repoUrl}/branches/{EscapePath(branchName)}/protection", token));
                if (lifted.IsSuccessStatusCode)
                {
                    response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Delete, refUrl, token));
                }
            }

            if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity))
            {
                response.EnsureSuccessStatusCode();
            }
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<bool> RepoHasCommitsAsync(long installationId, string owner, string repo)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var response = await _httpClient.SendAsync(
                CreateRequest(HttpMethod.Get, $"{RepoUrl(owner, repo)}/commits?per_page=1", token));

            // 409 is GitHub's "Git Repository is empty".
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            var commits = await response.Content.ReadFromJsonAsync<List<GitHubCompareCommitPayload>>();
            return commits is { Count: > 0 };
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<bool> EnsureMergeGateAsync(long installationId, bool isOrganization, string owner, string repo)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var repoUrl = RepoUrl(owner, repo);
            var repository = await GetAsync<GitHubRepoPayload>(repoUrl, token);
            var protectionUrl = $"{repoUrl}/branches/{EscapePath(repository.DefaultBranch)}/protection";
            var ours = new Dictionary<string, object?> { ["context"] = GitHubScopeCheck.Name, ["app_id"] = AppId() };

            var existing = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, protectionUrl, token));

            // A private repo on a free plan can't be protected, and says so already when asked about its rules.
            if (await IsPlanLimitAsync(existing))
            {
                return false;
            }

            HttpResponseMessage result;
            if (existing.StatusCode == HttpStatusCode.NotFound)
            {
                // No rules yet: the gate is the first one. Admins are held to it too, or the owner would walk around it.
                var create = CreateRequest(HttpMethod.Put, protectionUrl, token);
                create.Content = JsonContent.Create(new
                {
                    required_status_checks = new { strict = false, checks = new[] { ours } },
                    enforce_admins = true,
                    required_pull_request_reviews = (object?)null,
                    restrictions = (object?)null
                });
                result = await _httpClient.SendAsync(create);
            }
            else
            {
                existing.EnsureSuccessStatusCode();
                var current = (await existing.Content.ReadFromJsonAsync<GitHubBranchProtectionPayload>())?.RequiredStatusChecks;

                // Protected without status checks: adding one means rewriting every other rule, which we won't do blind.
                if (current is null)
                {
                    return false;
                }

                if (current.Checks.Any(c => c.Context == GitHubScopeCheck.Name && c.AppId == AppId()))
                {
                    return true;
                }

                var checks = current.Checks
                    .Select(c => c.AppId is null
                        ? new Dictionary<string, object?> { ["context"] = c.Context }
                        : new Dictionary<string, object?> { ["context"] = c.Context, ["app_id"] = c.AppId })
                    .Append(ours)
                    .ToList();
                var update = CreateRequest(HttpMethod.Patch, $"{protectionUrl}/required_status_checks", token);
                update.Content = JsonContent.Create(new { strict = current.Strict, checks });
                result = await _httpClient.SendAsync(update);
            }

            if (result.IsSuccessStatusCode)
            {
                return true;
            }

            // A personal repo on a plan without branch protection answers 403/404; nothing can be done there.
            if (!isOrganization && result.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                return false;
            }

            throw new GitHubRequestFailedException();
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task CreateScopeCheckRunAsync(
        long installationId, string owner, string repo, string headSha, string title, string summary)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var request = CreateRequest(HttpMethod.Post, $"{RepoUrl(owner, repo)}/check-runs", token);
            request.Content = JsonContent.Create(new
            {
                name = GitHubScopeCheck.Name,
                head_sha = headSha,
                status = "completed",
                conclusion = "success",
                output = new { title, summary }
            });

            (await _httpClient.SendAsync(request)).EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<bool> HasPassingScopeCheckAsync(long installationId, string owner, string repo, string headSha)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var url = $"{RepoUrl(owner, repo)}/commits/{Uri.EscapeDataString(headSha)}/check-runs"
                + $"?check_name={Uri.EscapeDataString(GitHubScopeCheck.Name)}&app_id={AppId()}";
            var checks = await GetAsync<GitHubCheckRunsPayload>(url, token);

            return checks.CheckRuns.Any(c => c.Conclusion == "success");
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<bool> InviteCollaboratorAsync(long installationId, string owner, string repo, string username)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var request = CreateRequest(
                HttpMethod.Put, $"{RepoUrl(owner, repo)}/collaborators/{Uri.EscapeDataString(username)}", token);
            request.Content = JsonContent.Create(new { permission = "push" });

            // 201: invitation sent. 204: already has access.
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return response.StatusCode == HttpStatusCode.Created;
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task RemoveCollaboratorAsync(long installationId, string owner, string repo, string username)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var repoUrl = RepoUrl(owner, repo);

            // 204 whether or not the user was a collaborator, so repeating it is harmless.
            var removal = CreateRequest(HttpMethod.Delete, $"{repoUrl}/collaborators/{Uri.EscapeDataString(username)}", token);
            (await _httpClient.SendAsync(removal)).EnsureSuccessStatusCode();

            // Removing the collaborator leaves an invitation they have not accepted yet standing: cancel it on its own.
            var invitations = await GetAsync<List<GitHubRepoInvitationPayload>>($"{repoUrl}/invitations?per_page=100", token);
            foreach (var invitation in invitations.Where(i =>
                         string.Equals(i.Invitee?.Login, username, StringComparison.OrdinalIgnoreCase)))
            {
                var cancel = CreateRequest(HttpMethod.Delete, $"{repoUrl}/invitations/{invitation.Id}", token);
                (await _httpClient.SendAsync(cancel)).EnsureSuccessStatusCode();
            }
        }
        catch (GitHubAppUninstalledException)
        {
            // The App is gone from the repository, so there is no access left that ShipMate could take away.
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // The repository itself is gone.
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    public async Task<IReadOnlyList<GitHubPullRequestRef>> GetMergedPullRequestsOfCommitAsync(
        long installationId, string owner, string repo, string commitSha)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var pulls = await GetAsync<List<GitHubCommitPullPayload>>(
                $"{RepoUrl(owner, repo)}/commits/{Uri.EscapeDataString(commitSha)}/pulls", token);

            return pulls.Where(p => p.MergedAt is not null).Select(p => new GitHubPullRequestRef(p.Number, p.Head.Sha)).ToList();
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    private long AppId() => long.Parse(_configuration["GitHubApp:AppId"]!);

    public async Task RemoveRepoLockAsync(long installationId, string owner, string repo)
    {
        try
        {
            var token = await GetInstallationTokenAsync(installationId);
            var repoUrl = RepoUrl(owner, repo);

            foreach (var ruleset in await FindFeatureLockRulesetsAsync(repoUrl, token))
            {
                var removal = await _httpClient.SendAsync(
                    CreateRequest(HttpMethod.Delete, $"{repoUrl}/rulesets/{ruleset.Id}", token));
                if (removal.StatusCode != HttpStatusCode.NotFound)
                {
                    removal.EnsureSuccessStatusCode();
                }
            }

            var repository = await GetAsync<GitHubRepoPayload>(repoUrl, token);
            var protectionUrl = $"{repoUrl}/branches/{EscapePath(repository.DefaultBranch)}/protection";
            var existing = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, protectionUrl, token));
            // No protection to take our check off: none set, or a plan that has none.
            if (existing.StatusCode == HttpStatusCode.NotFound || await IsPlanLimitAsync(existing))
            {
                return;
            }

            existing.EnsureSuccessStatusCode();
            var current = (await existing.Content.ReadFromJsonAsync<GitHubBranchProtectionPayload>())?.RequiredStatusChecks;
            if (current is null || !current.Checks.Any(IsOurCheck))
            {
                return;
            }

            // Only our check goes; any other check the owner requires stays.
            var others = current.Checks
                .Where(c => !IsOurCheck(c))
                .Select(c => c.AppId is null
                    ? new Dictionary<string, object?> { ["context"] = c.Context }
                    : new Dictionary<string, object?> { ["context"] = c.Context, ["app_id"] = c.AppId })
                .ToList();

            // The list is emptied, never the whole setting: a branch protected without status checks can't get the gate
            // back later without rewriting every other rule (see EnsureMergeGateAsync), so linking again would find it shut.
            var request = CreateRequest(HttpMethod.Patch, $"{protectionUrl}/required_status_checks", token);
            request.Content = JsonContent.Create(new { strict = current.Strict, checks = others });

            (await _httpClient.SendAsync(request)).EnsureSuccessStatusCode();
        }
        catch (GitHubAppUninstalledException)
        {
            // The App is gone from the repository, so there is nothing left that ShipMate could take off.
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // The repository itself is gone.
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }
    }

    // GitHub answers 403 "Upgrade to GitHub Pro or make this repository public" for branch rules on a private repo of a free
    // plan. Any other 403 is a real refusal and is not mistaken for it.
    private static async Task<bool> IsPlanLimitAsync(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Forbidden
        && (await response.Content.ReadAsStringAsync()).Contains("Upgrade to GitHub", StringComparison.OrdinalIgnoreCase);

    private bool IsOurCheck(GitHubRequiredCheckPayload check) =>
        check.Context == GitHubScopeCheck.Name && check.AppId == AppId();

    private const string FeatureLockRulesetName = "ShipMate feature branch lock";

    // One ruleset locks every feature/** branch for everyone but the Bot, admins and owners included (a protection rule
    // on a branch lets them through). Safe to repeat. False when the repository can't have rulesets, such as a private
    // repo on a free plan, which answers 403 or 404.
    private async Task<bool> EnsureFeatureLockRulesetAsync(string repoUrl, string token)
    {
        if ((await FindFeatureLockRulesetsAsync(repoUrl, token)).Count > 0)
        {
            return true;
        }

        var create = CreateRequest(HttpMethod.Post, $"{repoUrl}/rulesets", token);
        create.Content = JsonContent.Create(new
        {
            name = FeatureLockRulesetName,
            target = "branch",
            enforcement = "active",
            bypass_actors = new[] { new { actor_id = AppId(), actor_type = "Integration", bypass_mode = "always" } },
            conditions = new { ref_name = new { include = new[] { "refs/heads/feature/**" }, exclude = Array.Empty<string>() } },
            rules = new[] { new { type = "creation" }, new { type = "update" }, new { type = "deletion" }, new { type = "non_fast_forward" } }
        });
        var response = await _httpClient.SendAsync(create);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        // 422 is also what a claim racing this one gets for the name already taken.
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            return (await FindFeatureLockRulesetsAsync(repoUrl, token)).Count > 0;
        }

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    private async Task<List<GitHubRulesetPayload>> FindFeatureLockRulesetsAsync(string repoUrl, string token)
    {
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, $"{repoUrl}/rulesets", token));
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var all = await response.Content.ReadFromJsonAsync<List<GitHubRulesetPayload>>() ?? [];
        return all.Where(r => r.Name == FeatureLockRulesetName).ToList();
    }

    // Fallback only: a protection rule on one branch. Only the Bot may push, nobody may force-push or delete, and admins
    // are meant to be held to it too, but GitHub lets owners through the push restriction.
    // "Only the Bot" needs push restrictions, which GitHub offers on organization repositories only.
    private async Task ProtectBranchAsync(string repoUrl, string token, bool isOrganization, string branchName)
    {
        var request = CreateRequest(HttpMethod.Put, $"{repoUrl}/branches/{EscapePath(branchName)}/protection", token);
        request.Content = JsonContent.Create(new
        {
            required_status_checks = (object?)null,
            enforce_admins = true,
            required_pull_request_reviews = (object?)null,
            restrictions = isOrganization
                ? new { users = Array.Empty<string>(), teams = Array.Empty<string>(), apps = new[] { _configuration["GitHubApp:Slug"] } }
                : (object?)null,
            allow_force_pushes = false,
            allow_deletions = false
        });

        var response = await _httpClient.SendAsync(request);

        // A personal repo on a plan without branch protection answers 403/404. That repo can't be hard-locked anyway.
        var cannotProtectPersonalRepo = !isOrganization &&
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound;
        if (!response.IsSuccessStatusCode && !cannotProtectPersonalRepo)
        {
            throw new GitHubBranchCreationFailedException();
        }
    }

    private async Task<string> GetInstallationTokenAsync(long installationId)
    {
        var cacheKey = TokenCacheKey(installationId);
        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
        {
            return cached;
        }

        var request = CreateRequest(
            HttpMethod.Post, $"{ApiBase}/app/installations/{installationId}/access_tokens", CreateAppJwt());
        var response = await _httpClient.SendAsync(request);

        // GitHub forgets the installation once the App is uninstalled.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new GitHubAppUninstalledException();
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GitHubInstallationTokenPayload>()
            ?? throw new HttpRequestException("Empty installation token response from GitHub.");

        // GitHub tokens last an hour; drop ours a few minutes early so a request never carries an expired one.
        _cache.Set(cacheKey, payload.Token, payload.ExpiresAt.AddMinutes(-5));
        return payload.Token;
    }

    private static string TokenCacheKey(long installationId) => $"github-app-token-{installationId}";

    // The App proves who it is with a short-lived JWT signed by its private key.
    private string CreateAppJwt()
    {
        var appId = _configuration["GitHubApp:AppId"];
        var keyPath = _configuration["GitHubApp:PrivateKeyPath"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(keyPath))
        {
            throw new InvalidOperationException("GitHubApp:AppId and GitHubApp:PrivateKeyPath must be configured.");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(File.ReadAllText(keyPath));

        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = appId,
            // Backdated a minute so a small clock difference with GitHub doesn't reject it.
            IssuedAt = now.AddSeconds(-60),
            Expires = now.AddMinutes(9),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
            {
                // The key is disposed right after signing, so it must not stay in a provider cache.
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
            }
        };

        return new JwtSecurityTokenHandler().CreateEncodedJwt(descriptor);
    }

    private async Task<T> GetAsync<T>(string url, string token)
    {
        var response = await _httpClient.SendAsync(CreateRequest(HttpMethod.Get, url, token));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new HttpRequestException($"Empty response from GitHub for {url}.");
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string bearerToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("ShipMate");
        return request;
    }

    private static string RepoUrl(string owner, string repo) =>
        $"{ApiBase}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}";

    // Branch names contain slashes that must stay as path separators.
    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
