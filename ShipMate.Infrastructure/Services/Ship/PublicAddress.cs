using System.Net;
using System.Net.Sockets;

namespace ShipMate.Infrastructure.Services.Ship;

/// <summary>
/// Whether an IP address is on the public internet. SHIP calls a URL someone typed in from inside ShipMate's own
/// network, so every address that leads back inside (loopback, private ranges, the cloud metadata service, and the
/// IPv6 forms of all of them) must be refused before a connection is made.
/// </summary>
public static class PublicAddress
{
    private static readonly (uint Network, int PrefixLength)[] BlockedV4 =
    [
        (Parse("0.0.0.0"), 8),        // "this network"
        (Parse("10.0.0.0"), 8),       // private
        (Parse("100.64.0.0"), 10),    // carrier-grade NAT
        (Parse("127.0.0.0"), 8),      // loopback
        (Parse("169.254.0.0"), 16),   // link-local, including the cloud metadata service
        (Parse("172.16.0.0"), 12),    // private
        (Parse("192.0.0.0"), 24),     // IETF protocol assignments
        (Parse("192.0.2.0"), 24),     // documentation
        (Parse("192.88.99.0"), 24),   // 6to4 relay
        (Parse("192.168.0.0"), 16),   // private
        (Parse("198.18.0.0"), 15),    // benchmarking
        (Parse("198.51.100.0"), 24),  // documentation
        (Parse("203.0.113.0"), 24),   // documentation
        (Parse("224.0.0.0"), 4),      // multicast
        (Parse("240.0.0.0"), 4)       // reserved, including broadcast
    ];

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false
        };
    }

    private static bool IsPublicV4(IPAddress address)
    {
        var value = ToUInt32(address.GetAddressBytes());
        return !BlockedV4.Any(range => InRange(value, range.Network, range.PrefixLength));
    }

    private static bool IsPublicV6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        // Global unicast is 2000::/3. Everything outside it (loopback, unspecified, unique local fc00::/7,
        // link-local fe80::/10, multicast ff00::/8, NAT64 64:ff9b::/96 and the rest) is not a public host.
        if ((bytes[0] & 0xE0) != 0x20)
        {
            return false;
        }

        // 2001::/23: IETF protocol assignments, including Teredo tunnels, which reach IPv4 hosts indirectly.
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && (bytes[2] & 0xFE) == 0x00)
        {
            return false;
        }

        // 2001:db8::/32: documentation.
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
        {
            return false;
        }

        // 2002::/16 (6to4) carries an IPv4 address in its next four bytes: it is only as public as that address.
        if (bytes[0] == 0x20 && bytes[1] == 0x02)
        {
            return IsPublicV4(new IPAddress(bytes[2..6]));
        }

        return true;
    }

    private static bool InRange(uint value, uint network, int prefixLength)
    {
        var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
        return (value & mask) == (network & mask);
    }

    private static uint Parse(string address) => ToUInt32(IPAddress.Parse(address).GetAddressBytes());

    private static uint ToUInt32(byte[] bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
}
