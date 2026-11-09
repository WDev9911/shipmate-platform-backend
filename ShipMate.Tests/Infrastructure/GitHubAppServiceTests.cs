using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using ShipMate.Application.Exceptions;
using ShipMate.Infrastructure.Services.GitHub;

namespace ShipMate.Tests.Infrastructure;

public class GitHubAppServiceTests : IDisposable
{
    private readonly string _keyPath = Path.Combine(Path.GetTempPath(), $"shipmate-test-key-{Guid.NewGuid()}.pem");

    // Answers every request with the next scripted status; a 200 on the token endpoint carries a token.
    private sealed class ScriptedHandler(params (string UrlEnd, HttpStatusCode Status, string? Body)[] script) : HttpMessageHandler
    {
        private int _next;
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            var (urlEnd, status, scripted) = script[_next++];
            Assert.EndsWith(urlEnd, request.RequestUri!.AbsolutePath);
            var body = scripted ?? (urlEnd == "access_tokens"
                ? $$"""{"token":"token-{{_next}}","expires_at":"{{DateTime.UtcNow.AddHours(1):O}}"}"""
                : """{"object":{"sha":"abc123"}}""");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private GitHubAppService Service(ScriptedHandler handler)
    {
        using var rsa = RSA.Create(2048);
        File.WriteAllText(_keyPath, rsa.ExportRSAPrivateKeyPem());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GitHubApp:AppId"] = "1",
            ["GitHubApp:PrivateKeyPath"] = _keyPath
        }).Build();
        return new GitHubAppService(new HttpClient(handler), configuration, new MemoryCache(new MemoryCacheOptions()));
    }

    // The token is cached for up to an hour, and an uninstalled App takes its tokens with it: the first call after the
    // uninstall must say the App is gone, not fail once with a generic GitHub error.
    [Fact]
    public async Task GetBranchHead_WhenTheCachedTokenIsRefusedAndTheAppIsGone_SaysTheAppWasUninstalled()
    {
        var handler = new ScriptedHandler(
            ("access_tokens", HttpStatusCode.Created, null),   // first call fills the cache
            ("staging/x", HttpStatusCode.OK, null),
            ("staging/x", HttpStatusCode.Unauthorized, null),  // cached token refused after the uninstall
            ("access_tokens", HttpStatusCode.NotFound, null)); // GitHub no longer knows the installation
        var service = Service(handler);

        await service.GetBranchHeadShaAsync(7, "o", "r", "staging/x");

        await Assert.ThrowsAsync<GitHubAppUninstalledException>(() => service.GetBranchHeadShaAsync(7, "o", "r", "staging/x"));
    }

    [Fact]
    public async Task GetBranchHead_WhenTheCachedTokenExpiredButTheAppIsStillThere_RetriesWithANewToken()
    {
        var handler = new ScriptedHandler(
            ("access_tokens", HttpStatusCode.Created, null),
            ("staging/x", HttpStatusCode.OK, null),
            ("staging/x", HttpStatusCode.Unauthorized, null),
            ("access_tokens", HttpStatusCode.Created, null),
            ("staging/x", HttpStatusCode.OK, null));
        var service = Service(handler);

        await service.GetBranchHeadShaAsync(7, "o", "r", "staging/x");
        var sha = await service.GetBranchHeadShaAsync(7, "o", "r", "staging/x");

        Assert.Equal("abc123", sha);
    }

    private const string PlanLimitBody = """{"message":"Upgrade to GitHub Pro or make this repository public to enable this feature."}""";

    // A private repo of a free plan answers 403 as soon as its branch rules are read: it can't be protected, which is not
    // a failure of the link.
    [Fact]
    public async Task EnsureMergeGate_OnAPrivateRepoOfAFreePlan_SaysTheGateCouldNotBeSwitchedOn()
    {
        var handler = new ScriptedHandler(
            ("access_tokens", HttpStatusCode.Created, null),
            ("r", HttpStatusCode.OK, """{"name":"r","private":true,"default_branch":"main"}"""),
            ("protection", HttpStatusCode.Forbidden, PlanLimitBody));

        var enabled = await Service(handler).EnsureMergeGateAsync(7, isOrganization: false, "o", "r");

        Assert.False(enabled);
    }

    // Any other 403 is a real refusal, not a plan without branch rules.
    [Fact]
    public async Task EnsureMergeGate_WhenGitHubRefusesForAnotherReason_StillFails()
    {
        var handler = new ScriptedHandler(
            ("access_tokens", HttpStatusCode.Created, null),
            ("r", HttpStatusCode.OK, """{"name":"r","private":false,"default_branch":"main"}"""),
            ("protection", HttpStatusCode.Forbidden, """{"message":"Resource not accessible by integration"}"""));

        await Assert.ThrowsAsync<GitHubRequestFailedException>(() =>
            Service(handler).EnsureMergeGateAsync(7, isOrganization: false, "o", "r"));
    }

    // Unlinking such a repo must not fail either: there is nothing of ShipMate's to take off.
    [Fact]
    public async Task RemoveRepoLock_OnAPrivateRepoOfAFreePlan_HasNothingToRemoveAndDoesNotFail()
    {
        var handler = new ScriptedHandler(
            ("access_tokens", HttpStatusCode.Created, null),
            ("rulesets", HttpStatusCode.Forbidden, PlanLimitBody),
            ("r", HttpStatusCode.OK, """{"name":"r","private":true,"default_branch":"main"}"""),
            ("protection", HttpStatusCode.Forbidden, PlanLimitBody));

        await Service(handler).RemoveRepoLockAsync(7, "o", "r");
    }

    public void Dispose() => File.Delete(_keyPath);
}
