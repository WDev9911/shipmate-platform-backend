using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ShipMate.Infrastructure.Services;

namespace ShipMate.Tests.Infrastructure;

public class ResendEmailSenderTests
{
    private sealed class FixedStatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("network down");
    }

    private static ResendEmailSender Sender(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://resend.test/") },
            new ConfigurationBuilder().Build(),
            NullLogger<ResendEmailSender>.Instance);

    [Fact]
    public async Task TrySend_ReturnsTrue_WhenResendAccepts() =>
        Assert.True(await Sender(new FixedStatusHandler(HttpStatusCode.OK)).TrySendAsync("a@b.com", "s", "<p>x</p>"));

    [Fact]
    public async Task TrySend_ReturnsFalseWithoutThrowing_WhenResendRejects() =>
        Assert.False(await Sender(new FixedStatusHandler((HttpStatusCode)422)).TrySendAsync("a@example.com", "s", "<p>x</p>"));

    [Fact]
    public async Task TrySend_ReturnsFalseWithoutThrowing_WhenNetworkFails() =>
        Assert.False(await Sender(new ThrowingHandler()).TrySendAsync("a@b.com", "s", "<p>x</p>"));

    // The sender still throws on a refused mail; AuthService turns that into EMAIL_DELIVERY_FAILED for verification codes.
    [Fact]
    public async Task Send_StillThrows_WhenResendRejects() =>
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Sender(new FixedStatusHandler((HttpStatusCode)422)).SendAsync("a@example.com", "s", "<p>x</p>"));
}
