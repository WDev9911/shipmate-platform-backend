using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Policies;

namespace ShipMate.Infrastructure.Services.Ship;

/// <summary>
/// Checks a production URL for SHIP: it answers 200 over valid TLS, and the same host serves the workspace's
/// ownership token. The server makes these calls from inside ShipMate's network, so it never connects anywhere
/// that isn't a public address: every hop is checked before it is requested, and again on the address the
/// connection is actually opened to, so a DNS answer that changes in between can't lead inside.
/// </summary>
public sealed class ProductionUrlChecker : IProductionUrlChecker
{
    public const int MaxRedirects = 3;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(30);

    private const string UserAgent = "ShipMate-ShipCheck/1.0";

    private readonly IHostAddressResolver _resolver;
    private readonly Func<TlsObservations, HttpMessageHandler> _handlerFactory;

    public ProductionUrlChecker(IHostAddressResolver resolver)
        : this(resolver, null)
    {
    }

    /// <param name="handlerFactory">Tests pass a handler that answers without a network; null builds the real one.</param>
    public ProductionUrlChecker(IHostAddressResolver resolver, Func<TlsObservations, HttpMessageHandler>? handlerFactory)
    {
        _resolver = resolver;
        _handlerFactory = handlerFactory ?? CreateHandler;
    }

    public async Task<ProductionUrlCheckResult> CheckAsync(Uri url, string ownershipToken, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TotalTimeout);

        var tls = new TlsObservations();
        using var handler = _handlerFactory(tls);
        using var client = new HttpMessageInvoker(handler, disposeHandler: false);

        var page = await FetchAsync(client, url, sameHostOnly: false, maxBodyBytes: 0, budget.Token, cancellationToken);
        if (page.Error == ShipCheckCodes.HostNotPublic)
        {
            return new ProductionUrlCheckResult(
                null, ShipCheckCodes.HostNotPublic, false, ShipCheckCodes.NotChecked, null, false, ShipCheckCodes.Unreachable);
        }

        var (sslValid, sslError, sslExpiresAt) = SslOf(page, tls);

        // On the host the team entered, never where a redirect leads: an open redirect on someone else's site
        // must not let a team prove that site is theirs.
        var file = await FetchAsync(
            client, new Uri(url, ShipOwnership.FilePath), sameHostOnly: true, ShipOwnership.MaxFileBytes, budget.Token, cancellationToken);
        var (ownershipVerified, ownershipError) = OwnershipOf(file, ownershipToken);

        return new ProductionUrlCheckResult(
            page.Status,
            page.Error ?? (page.Status == 200 ? null : ShipCheckCodes.UnexpectedStatus),
            sslValid,
            sslError,
            sslExpiresAt,
            ownershipVerified,
            ownershipError);
    }

    private sealed record Fetch(int? Status, string? Error, string? Host, string? Body);

    private async Task<Fetch> FetchAsync(
        HttpMessageInvoker client, Uri start, bool sameHostOnly, int maxBodyBytes,
        CancellationToken budget, CancellationToken caller)
    {
        var current = start;

        for (var redirects = 0; ; redirects++)
        {
            // A redirect can point anywhere, so each hop meets the same rules as the URL the team entered.
            if (ProductionUrlPolicy.Check(current) != ProductionUrlError.None)
            {
                return new Fetch(null, ShipCheckCodes.RedirectNotAllowed, current.IdnHost, null);
            }

            var resolveError = await CheckHostAsync(current.IdnHost, budget);
            if (resolveError is not null)
            {
                return new Fetch(null, resolveError, current.IdnHost, null);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(budget);
            timeout.CancelAfter(RequestTimeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException) when (!caller.IsCancellationRequested)
            {
                return new Fetch(null, ShipCheckCodes.Timeout, current.IdnHost, null);
            }
            catch (HttpRequestException ex)
            {
                return new Fetch(null, Classify(ex), current.IdnHost, null);
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                if (IsRedirect(status) && response.Headers.Location is { } location)
                {
                    if (redirects == MaxRedirects)
                    {
                        return new Fetch(status, ShipCheckCodes.TooManyRedirects, current.IdnHost, null);
                    }

                    var next = WithoutFragment(ResolveLocation(current, location));
                    if (sameHostOnly && !SameOrigin(next, start))
                    {
                        return new Fetch(status, ShipCheckCodes.RedirectedToOtherHost, current.IdnHost, null);
                    }

                    current = next;
                    continue;
                }

                string? body = null;
                if (maxBodyBytes > 0 && status == 200)
                {
                    try
                    {
                        body = await ReadAtMostAsync(response, maxBodyBytes, timeout.Token);
                    }
                    catch (OperationCanceledException) when (!caller.IsCancellationRequested)
                    {
                        return new Fetch(null, ShipCheckCodes.Timeout, current.IdnHost, null);
                    }
                    catch (Exception ex) when (ex is HttpRequestException or IOException)
                    {
                        return new Fetch(null, ShipCheckCodes.ConnectionFailed, current.IdnHost, null);
                    }
                }

                return new Fetch(status, null, current.IdnHost, body);
            }
        }
    }

    // Null when the host only leads to public addresses; otherwise why it can't be called.
    private async Task<string?> CheckHostAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            await ResolvePublicAsync(host, cancellationToken);
            return null;
        }
        catch (HostNotPublicException)
        {
            return ShipCheckCodes.HostNotPublic;
        }
        catch (SocketException)
        {
            return ShipCheckCodes.HostNotFound;
        }
        catch (OperationCanceledException)
        {
            return ShipCheckCodes.Timeout;
        }
    }

    // Every address must be public, not just one: otherwise the connection could pick the one that isn't.
    private async Task<IPAddress[]> ResolvePublicAsync(string host, CancellationToken cancellationToken)
    {
        var addresses = await _resolver.ResolveAsync(host, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        if (addresses.Any(a => !PublicAddress.IsPublic(a)))
        {
            throw new HostNotPublicException(host);
        }

        return addresses;
    }

    private (bool Valid, string? Error, DateTime? ExpiresAt) SslOf(Fetch page, TlsObservations tls)
    {
        var seen = (page.Host is null ? null : tls.For(page.Host)) ?? tls.Last;

        if (page.Error == ShipCheckCodes.TlsFailed)
        {
            return (false, seen?.Error ?? ShipCheckCodes.CertificateInvalid, seen?.ExpiresAt);
        }

        // No handshake happened (the name doesn't exist, the connection was refused...), so there is nothing to judge.
        if (seen is null)
        {
            return (false, ShipCheckCodes.NotChecked, null);
        }

        return (seen.Error is null, seen.Error, seen.ExpiresAt);
    }

    private static (bool Verified, string? Error) OwnershipOf(Fetch file, string token) => file switch
    {
        { Error: ShipCheckCodes.RedirectedToOtherHost } => (false, ShipCheckCodes.RedirectedToOtherHost),
        { Error: not null } => (false, ShipCheckCodes.Unreachable),
        { Status: not 200 } => (false, ShipCheckCodes.FileNotFound),
        _ when ShipOwnership.FileProves(file.Body ?? string.Empty, token) => (true, null),
        _ => (false, ShipCheckCodes.TokenMismatch)
    };

    private static string Classify(HttpRequestException ex)
    {
        for (Exception? inner = ex; inner is not null; inner = inner.InnerException)
        {
            switch (inner)
            {
                case HostNotPublicException:
                    return ShipCheckCodes.HostNotPublic;
                case AuthenticationException:
                    return ShipCheckCodes.TlsFailed;
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData }:
                    return ShipCheckCodes.HostNotFound;
            }
        }

        return ShipCheckCodes.ConnectionFailed;
    }

    private static bool IsRedirect(int status) => status is 301 or 302 or 303 or 307 or 308;

    private static bool SameOrigin(Uri a, Uri b) =>
        string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port && a.Scheme == b.Scheme;

    private static Uri WithoutFragment(Uri uri) => new(uri.GetLeftPart(UriPartial.Query));

    // On Linux and macOS, .NET reads "Location: /login" as the file path /login rather than a relative URL.
    private static Uri ResolveLocation(Uri current, Uri location) =>
        location.IsAbsoluteUri && !(location.IsFile && location.OriginalString.StartsWith('/'))
            ? location
            : new Uri(current, location.OriginalString);

    private static async Task<string> ReadAtMostAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maxBytes];
        var read = 0;
        while (read < maxBytes)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, maxBytes - read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return Encoding.UTF8.GetString(buffer, 0, read);
    }

    private HttpMessageHandler CreateHandler(TlsObservations tls) => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = RequestTimeout,
        MaxResponseHeadersLength = 64,
        ConnectCallback = ConnectToPublicAddressAsync,
        SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
            {
                tls.Record((sender as SslStream)?.TargetHostName, TlsObservation.Of(certificate, chain, errors));
                return errors == SslPolicyErrors.None;
            }
        }
    };

    // Resolves again and connects only to the addresses it just checked, so the name can't be re-pointed at an
    // internal address between the check before the request and the connection itself.
    private async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await ResolvePublicAsync(context.DnsEndPoint.Host, cancellationToken);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private sealed class HostNotPublicException : Exception
    {
        public HostNotPublicException(string host)
            : base($"'{host}' leads to an address that is not on the public internet.")
        {
        }
    }
}

/// <summary>What the TLS handshake found about one host's certificate. A null error means it was valid.</summary>
public sealed record TlsObservation(string? Error, DateTime? ExpiresAt)
{
    public static TlsObservation Of(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        DateTime? expiresAt = certificate is null
            ? null
            : (certificate as X509Certificate2 ?? new X509Certificate2(certificate)).NotAfter.ToUniversalTime();

        if (errors == SslPolicyErrors.None)
        {
            return new TlsObservation(null, expiresAt);
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
        {
            return new TlsObservation(ShipCheckCodes.CertificateMissing, expiresAt);
        }

        var chainStatus = chain?.ChainStatus.Aggregate(X509ChainStatusFlags.NoError, (all, s) => all | s.Status)
            ?? X509ChainStatusFlags.NoError;

        if (chainStatus.HasFlag(X509ChainStatusFlags.NotTimeValid) || expiresAt < DateTime.UtcNow)
        {
            return new TlsObservation(ShipCheckCodes.CertificateExpired, expiresAt);
        }

        if (errors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return new TlsObservation(ShipCheckCodes.CertificateNameMismatch, expiresAt);
        }

        if (chainStatus.HasFlag(X509ChainStatusFlags.UntrustedRoot) || chainStatus.HasFlag(X509ChainStatusFlags.PartialChain))
        {
            return new TlsObservation(ShipCheckCodes.CertificateUntrusted, expiresAt);
        }

        return new TlsObservation(ShipCheckCodes.CertificateInvalid, expiresAt);
    }
}

/// <summary>The certificates seen during one check, by host. Handshakes happen inside the HTTP handler, so they are collected here.</summary>
public sealed class TlsObservations
{
    private readonly ConcurrentDictionary<string, TlsObservation> _byHost = new(StringComparer.OrdinalIgnoreCase);

    public TlsObservation? Last { get; private set; }

    public void Record(string? host, TlsObservation observation)
    {
        if (!string.IsNullOrEmpty(host))
        {
            _byHost[host] = observation;
        }

        Last = observation;
    }

    public TlsObservation? For(string host) => _byHost.TryGetValue(host, out var observation) ? observation : null;
}
