using System.Net;

namespace ShipMate.Infrastructure.Services.Ship;

/// <summary>Turns a host name into addresses. Behind an interface so the URL checks can be tested without DNS.</summary>
public interface IHostAddressResolver
{
    /// <exception cref="System.Net.Sockets.SocketException">The name doesn't exist.</exception>
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        // An IPv6 literal can arrive in its URL form, between brackets.
        var name = host.Trim('[', ']');
        return IPAddress.TryParse(name, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(name, cancellationToken);
    }
}
