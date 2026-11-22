using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Policies;
using ShipMate.Infrastructure.Services.Ship;

namespace ShipMate.Tests.Infrastructure;

public class ProductionUrlCheckerTests
{
    private const string Token = "shipmate-0123456789abcdef0123456789abcdef";
    private static readonly DateTime CertificateExpiry = new(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Uri Site = new("https://booking.example/app?ref=1");

    private readonly FakeResolver _resolver = new();
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new();
    private readonly List<Uri> _requested = new();
    private readonly Dictionary<string, Exception> _failures = new();
    private readonly Dictionary<string, TlsObservation> _certificates = new(StringComparer.OrdinalIgnoreCase);

    // Hosts resolve to a public address unless a test says otherwise.
    private sealed class FakeResolver : IHostAddressResolver
    {
        public Dictionary<string, Func<IPAddress[]>> Answers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Asked { get; } = new();

        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            Asked.Add(host);
            return Task.FromResult(Answers.TryGetValue(host, out var answer) ? answer() : [IPAddress.Parse("93.184.216.34")]);
        }
    }

    // Answers from the routes; reports a valid certificate for each host it serves, as the real handshake would.
    private sealed class FakeHandler(ProductionUrlCheckerTests test, TlsObservations tls) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!;
            test._requested.Add(url);

            test._certificates.TryGetValue(url.IdnHost, out var certificate);
            tls.Record(url.IdnHost, certificate ?? new TlsObservation(null, CertificateExpiry));

            if (test._failures.TryGetValue(url.GetLeftPart(UriPartial.Path), out var failure))
            {
                throw failure;
            }

            return Task.FromResult(test._routes.TryGetValue(url.GetLeftPart(UriPartial.Path), out var respond)
                ? respond()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private ProductionUrlChecker Checker() => new(_resolver, tls => new FakeHandler(this, tls));

    private void Serve(string url, HttpStatusCode status = HttpStatusCode.OK, string body = "<html></html>") =>
        _routes[url] = () => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8) };

    private void Redirect(string from, string to, HttpStatusCode status = HttpStatusCode.Found) =>
        _routes[from] = () => new HttpResponseMessage(status) { Headers = { Location = new Uri(to, UriKind.RelativeOrAbsolute) } };

    private void ServeToken(string host = "booking.example", string content = Token) =>
        Serve($"https://{host}{ShipOwnership.FilePath}", body: content);

    private Task<ProductionUrlCheckResult> Check(Uri? url = null) => Checker().CheckAsync(url ?? Site, Token, CancellationToken.None);

    // ---- everything right ----

    [Fact]
    public async Task AWorkingSiteThatServesTheToken_PassesEveryCheck()
    {
        Serve("https://booking.example/app");
        ServeToken();

        var result = await Check();

        Assert.Null(result.FailureCode);
        Assert.Equal(200, result.PingStatusCode);
        Assert.Null(result.PingError);
        Assert.True(result.SslValid);
        Assert.Equal(CertificateExpiry, result.SslExpiresAt);
        Assert.True(result.OwnershipVerified);
    }

    [Fact]
    public async Task TheTokenIsLookedForAtTheFixedPath_OnTheHostEntered_WhateverThePageUrl()
    {
        Serve("https://booking.example/app");
        ServeToken();

        await Check();

        Assert.Contains(new Uri("https://booking.example/.well-known/shipmate.txt"), _requested);
    }

    [Fact]
    public async Task RedirectsAreFollowed_UpToTheLimit()
    {
        Redirect("https://booking.example/app", "/login", HttpStatusCode.MovedPermanently);
        Redirect("https://booking.example/login", "https://www.booking.example/login");
        Serve("https://www.booking.example/login");
        ServeToken();

        var result = await Check();

        Assert.Null(result.FailureCode);
        Assert.Equal(200, result.PingStatusCode);
    }

    // ---- never reaching inside ----

    [Fact]
    public async Task AHostThatResolvesToAPrivateAddress_IsNeverCalled()
    {
        _resolver.Answers["booking.example"] = () => [IPAddress.Parse("10.0.0.5")];

        var result = await Check();

        Assert.Equal(ShipCheckCodes.UrlNotPublic, result.FailureCode);
        Assert.Equal(ShipCheckCodes.HostNotPublic, result.PingError);
        Assert.False(result.OwnershipVerified);
        Assert.Empty(_requested);
    }

    [Fact]
    public async Task AHostWithOnePrivateAddressAmongPublicOnes_IsRefused()
    {
        _resolver.Answers["booking.example"] = () => [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("169.254.169.254")];

        var result = await Check();

        Assert.Equal(ShipCheckCodes.UrlNotPublic, result.FailureCode);
        Assert.Empty(_requested);
    }

    [Fact]
    public async Task ARedirectTowardsAPrivateAddress_IsNotFollowed()
    {
        _resolver.Answers["admin.internal.example"] = () => [IPAddress.Parse("127.0.0.1")];
        Redirect("https://booking.example/app", "https://admin.internal.example/");

        var result = await Check();

        Assert.Equal(ShipCheckCodes.UrlNotPublic, result.FailureCode);
        Assert.DoesNotContain(_requested, u => u.Host == "admin.internal.example");
    }

    [Theory]
    [InlineData("http://booking.example/app")]
    [InlineData("https://booking.example:8080/")]
    [InlineData("https://localhost/")]
    [InlineData("file:///etc/passwd")]
    public async Task ARedirectToSomethingThatIsNotAPublicHttpsAddress_IsNotFollowed(string target)
    {
        Redirect("https://booking.example/app", target);

        var result = await Check();

        Assert.Equal(ShipCheckCodes.PingFailed, result.FailureCode);
        Assert.Equal(ShipCheckCodes.RedirectNotAllowed, result.PingError);
        Assert.Single(_requested, u => u.Host == "booking.example" && u.AbsolutePath == "/app");
    }

    [Fact]
    public async Task TooManyRedirects_Fail()
    {
        Redirect("https://booking.example/app", "/a");
        Redirect("https://booking.example/a", "/b");
        Redirect("https://booking.example/b", "/c");
        Redirect("https://booking.example/c", "/d");
        Serve("https://booking.example/d");

        var result = await Check();

        Assert.Equal(ShipCheckCodes.TooManyRedirects, result.PingError);
        Assert.DoesNotContain(_requested, u => u.AbsolutePath == "/d");
    }

    // ---- the page ----

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task APageThatDoesNotAnswer200_FailsThePing(HttpStatusCode status)
    {
        Serve("https://booking.example/app", status);
        ServeToken();

        var result = await Check();

        Assert.Equal(ShipCheckCodes.PingFailed, result.FailureCode);
        Assert.Equal((int)status, result.PingStatusCode);
        Assert.Equal(ShipCheckCodes.UnexpectedStatus, result.PingError);
    }

    [Fact]
    public async Task AHostThatDoesNotExist_FailsThePing()
    {
        _resolver.Answers["booking.example"] = () => throw new SocketException((int)SocketError.HostNotFound);

        var result = await Check();

        Assert.Equal(ShipCheckCodes.PingFailed, result.FailureCode);
        Assert.Equal(ShipCheckCodes.HostNotFound, result.PingError);
        Assert.Equal(ShipCheckCodes.NotChecked, result.SslError);
    }

    [Fact]
    public async Task APageThatNeverAnswers_TimesOut()
    {
        _failures["https://booking.example/app"] = new TaskCanceledException();
        ServeToken();

        var result = await Check();

        Assert.Equal(ShipCheckCodes.PingFailed, result.FailureCode);
        Assert.Equal(ShipCheckCodes.Timeout, result.PingError);
    }

    [Fact]
    public async Task ARefusedConnection_FailsThePing()
    {
        _failures["https://booking.example/app"] = new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused));

        var result = await Check();

        Assert.Equal(ShipCheckCodes.ConnectionFailed, result.PingError);
    }

    [Fact]
    public async Task AnExpiredCertificate_FailsTheSslCheck_AndSaysWhy()
    {
        _certificates["booking.example"] = new TlsObservation(ShipCheckCodes.CertificateExpired, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _failures["https://booking.example/app"] = new HttpRequestException("ssl", new AuthenticationException("expired"));
        _failures["https://booking.example/.well-known/shipmate.txt"] = new HttpRequestException("ssl", new AuthenticationException("expired"));

        var result = await Check();

        Assert.Equal(ShipCheckCodes.SslInvalid, result.FailureCode);
        Assert.Equal(ShipCheckCodes.TlsFailed, result.PingError);
        Assert.False(result.SslValid);
        Assert.Equal(ShipCheckCodes.CertificateExpired, result.SslError);
        Assert.Equal(ShipCheckCodes.Unreachable, result.OwnershipError);
    }

    // ---- ownership ----

    [Fact]
    public async Task NoTokenFile_FailsOwnership()
    {
        Serve("https://booking.example/app");

        var result = await Check();

        Assert.Equal(ShipCheckCodes.OwnershipUnverified, result.FailureCode);
        Assert.Equal(ShipCheckCodes.FileNotFound, result.OwnershipError);
        Assert.Equal(200, result.PingStatusCode);
        Assert.True(result.SslValid);
    }

    [Fact]
    public async Task AnotherWorkspacesToken_FailsOwnership()
    {
        Serve("https://booking.example/app");
        ServeToken(content: "shipmate-ffffffffffffffffffffffffffffffff");

        var result = await Check();

        Assert.Equal(ShipCheckCodes.TokenMismatch, result.OwnershipError);
    }

    [Fact]
    public async Task TheTokenAmongOtherLines_ProvesOwnership()
    {
        Serve("https://booking.example/app");
        ServeToken(content: "shipmate-ffffffffffffffffffffffffffffffff\r\n  " + Token + "  \r\n");

        Assert.True((await Check()).OwnershipVerified);
    }

    [Fact]
    public async Task OnlyTheBeginningOfTheFileIsRead()
    {
        Serve("https://booking.example/app");
        ServeToken(content: new string('x', ShipOwnership.MaxFileBytes) + "\n" + Token);

        Assert.Equal(ShipCheckCodes.TokenMismatch, (await Check()).OwnershipError);
    }

    [Fact]
    public async Task ATokenFileThatRedirectsToAnotherHost_ProvesNothing()
    {
        Serve("https://booking.example/app");
        Redirect("https://booking.example/.well-known/shipmate.txt", "https://attacker.example/.well-known/shipmate.txt");
        ServeToken(host: "attacker.example");

        var result = await Check();

        Assert.Equal(ShipCheckCodes.RedirectedToOtherHost, result.OwnershipError);
        Assert.DoesNotContain(_requested, u => u.Host == "attacker.example");
    }

    [Fact]
    public async Task ATokenFileRedirectedOnTheSameHost_IsFollowed()
    {
        Serve("https://booking.example/app");
        Redirect("https://booking.example/.well-known/shipmate.txt", "/static/shipmate.txt");
        Serve("https://booking.example/static/shipmate.txt", body: Token);

        Assert.True((await Check()).OwnershipVerified);
    }

    [Fact]
    public async Task APageThatRedirectsElsewhere_StillNeedsTheTokenOnTheHostEntered()
    {
        // The open-redirect trick: a page on a site the team doesn't own sends visitors to the team's site.
        Redirect("https://booking.example/app", "https://team.example/");
        Serve("https://team.example/");
        ServeToken(host: "team.example");

        var result = await Check();

        Assert.Equal(200, result.PingStatusCode);
        Assert.Equal(ShipCheckCodes.OwnershipUnverified, result.FailureCode);
    }

    // ---- the caller gives up ----

    [Fact]
    public async Task WhenTheRequestIsCancelled_TheCheckStops()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        _failures["https://booking.example/app"] = new TaskCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Checker().CheckAsync(Site, Token, cancelled.Token));
    }
}
