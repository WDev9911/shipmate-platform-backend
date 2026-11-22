using System.Net;
using ShipMate.Infrastructure.Services.Ship;

namespace ShipMate.Tests.Infrastructure;

public class PublicAddressTests
{
    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.255.255")]   // just below 172.16/12
    [InlineData("172.32.0.1")]       // just above it
    [InlineData("100.63.255.255")]   // just below carrier-grade NAT
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("2002:5db8:d822::1")] // 6to4 around a public IPv4 address
    public void PublicAddresses_AreAllowed(string address)
    {
        Assert.True(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("127.0.0.1")]         // loopback
    [InlineData("127.10.20.30")]
    [InlineData("0.0.0.0")]           // "this network"
    [InlineData("10.0.0.5")]          // private
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // cloud metadata service
    [InlineData("100.64.0.1")]        // carrier-grade NAT
    [InlineData("192.0.2.10")]        // documentation
    [InlineData("198.18.0.1")]        // benchmarking
    [InlineData("224.0.0.1")]         // multicast
    [InlineData("255.255.255.255")]   // broadcast
    [InlineData("::1")]               // IPv6 loopback
    [InlineData("::")]                // unspecified
    [InlineData("fe80::1")]           // link-local
    [InlineData("fc00::1")]           // unique local
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]           // multicast
    [InlineData("::ffff:127.0.0.1")]  // IPv4 loopback written as IPv6
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a00:1")]    // NAT64 around 10.0.0.1
    [InlineData("2001:db8::1")]       // documentation
    [InlineData("2001::1")]           // Teredo
    [InlineData("2002:7f00:1::1")]    // 6to4 around 127.0.0.1
    [InlineData("2002:a9fe:a9fe::1")] // 6to4 around the metadata service
    public void AddressesThatLeadInside_AreRefused(string address)
    {
        Assert.False(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }
}
