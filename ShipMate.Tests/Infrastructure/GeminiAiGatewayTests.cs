using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Infrastructure.Services.Ai;

namespace ShipMate.Tests.Infrastructure;

// Every way out of a Gemini call leaves exactly one log line with the right outcome, and a cancellation stays a
// cancellation (it is not turned into "provider unavailable", so BUILD can run the analysis again after a restart).
public class GeminiAiGatewayTests
{
    private const string Model = "gemini-configured";
    private readonly List<AiCallLog> _logged = [];
    private readonly IAiCallLogWriter _writer = Substitute.For<IAiCallLogWriter>();

    public GeminiAiGatewayTests()
    {
        _writer.WriteAsync(Arg.Do<AiCallLog>(_logged.Add)).Returns(Task.CompletedTask);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            answer(request, cancellationToken);
    }

    private GeminiAiGateway Gateway(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer) =>
        new(new HttpClient(new FakeHandler(answer)) { BaseAddress = new Uri("https://gemini.test/") },
            Options.Create(new GeminiOptions { Model = Model, ApiKey = "k" }), _writer, Substitute.For<ILogger<GeminiAiGateway>>());

    private GeminiAiGateway Answering(HttpStatusCode status, string body) =>
        Gateway((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") }));

    private static AiJsonRequest Request(AiCallPurpose purpose = AiCallPurpose.BuildAnalysis) =>
        new() { Purpose = purpose, SystemInstruction = "s", UserPrompt = "u", JsonSchema = "{\"type\":\"object\"}" };

    private const string Good = """
        {
          "candidates": [ { "content": { "parts": [ { "text": "thinking", "thought": true }, { "text": "{\"ok\":true}" } ] }, "finishReason": "STOP" } ],
          "modelVersion": "gemini-2.5-flash-001",
          "usageMetadata": { "promptTokenCount": 120, "candidatesTokenCount": 30, "thoughtsTokenCount": 15 }
        }
        """;

    [Fact]
    public async Task Success_ReturnsTheJson_AndLogsTheModelTokensAndPurpose()
    {
        var response = await Answering(HttpStatusCode.OK, Good).GenerateJsonAsync(Request(AiCallPurpose.ComplexityScoring));

        Assert.Equal("{\"ok\":true}", response.Json);
        var log = Assert.Single(_logged);
        Assert.Equal(AiCallOutcome.Succeeded, log.Outcome);
        Assert.Equal(AiCallPurpose.ComplexityScoring, log.Purpose);
        Assert.Equal("gemini-2.5-flash-001", log.Model);
        Assert.Equal(120, log.InputTokens);
        Assert.Equal(30, log.OutputTokens);
        Assert.Equal(15, log.ThinkingTokens);
        Assert.True(log.DurationMs >= 0);
    }

    [Fact]
    public async Task Success_WithoutUsageOrModelVersion_LogsTheConfiguredModelAndNoTokens()
    {
        const string bare = """{ "candidates": [ { "content": { "parts": [ { "text": "{}" } ] }, "finishReason": "STOP" } ] }""";

        await Answering(HttpStatusCode.OK, bare).GenerateJsonAsync(Request());

        var log = Assert.Single(_logged);
        Assert.Equal(Model, log.Model);
        Assert.Null(log.InputTokens);
        Assert.Null(log.OutputTokens);
        Assert.Null(log.ThinkingTokens);
    }

    [Fact]
    public async Task TooManyRequests_IsRateLimited()
    {
        await Assert.ThrowsAsync<AiRateLimitedException>(() =>
            Answering(HttpStatusCode.TooManyRequests, "{}").GenerateJsonAsync(Request()));

        var log = Assert.Single(_logged);
        Assert.Equal(AiCallOutcome.RateLimited, log.Outcome);
        Assert.Equal(Model, log.Model);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AnErrorStatus_IsProviderUnavailable(HttpStatusCode status)
    {
        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            Answering(status, "{\"error\":\"x\"}").GenerateJsonAsync(Request()));

        Assert.Equal(AiCallOutcome.ProviderUnavailable, Assert.Single(_logged).Outcome);
    }

    [Fact]
    public async Task ANetworkError_IsProviderUnavailable()
    {
        var gateway = Gateway((_, _) => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => gateway.GenerateJsonAsync(Request()));

        Assert.Equal(AiCallOutcome.ProviderUnavailable, Assert.Single(_logged).Outcome);
    }

    [Fact]
    public async Task ATimeout_NobodyCancelled_IsProviderUnavailable()
    {
        var gateway = Gateway((_, _) => throw new TaskCanceledException("HttpClient.Timeout"));

        await Assert.ThrowsAsync<AiProviderUnavailableException>(() => gateway.GenerateJsonAsync(Request()));

        Assert.Equal(AiCallOutcome.ProviderUnavailable, Assert.Single(_logged).Outcome);
    }

    // L9: shutting down the app in the middle of a BUILD analysis used to mark the submission Failed.
    [Fact]
    public async Task ACancellationAskedForByTheCaller_StaysACancellation_AndIsLoggedAsCancelled()
    {
        using var cancel = new CancellationTokenSource();
        var gateway = Gateway(async (_, token) =>
        {
            cancel.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.GenerateJsonAsync(Request(), cancel.Token));

        Assert.IsNotType<AiProviderUnavailableException>(ex);
        Assert.Equal(AiCallOutcome.Cancelled, Assert.Single(_logged).Outcome);
    }

    [Fact]
    public async Task ABodyThatIsNotJson_IsProviderUnavailable_NotARawJsonError()
    {
        await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            Answering(HttpStatusCode.OK, "<html>bad gateway</html>").GenerateJsonAsync(Request()));

        Assert.Equal(AiCallOutcome.ProviderUnavailable, Assert.Single(_logged).Outcome);
    }

    [Theory]
    [InlineData("""{ "candidates": [ { "content": { "parts": [ { "text": "{\"cut" } ] }, "finishReason": "MAX_TOKENS" } ], "usageMetadata": { "promptTokenCount": 9 } }""")]
    [InlineData("""{ "candidates": [ { "content": { "parts": [ { "text": "  " } ] }, "finishReason": "STOP" } ] }""")]
    [InlineData("""{ "promptFeedback": { "blockReason": "SAFETY" } }""")]
    public async Task NoUsableOutput_IsInvalidOutput(string body)
    {
        await Assert.ThrowsAsync<AiOutputInvalidException>(() => Answering(HttpStatusCode.OK, body).GenerateJsonAsync(Request()));

        Assert.Equal(AiCallOutcome.InvalidOutput, Assert.Single(_logged).Outcome);
    }

    [Fact]
    public async Task InvalidOutput_StillKeepsTheTokensItCost()
    {
        const string body = """{ "candidates": [ { "content": { "parts": [ { "text": "{\"cut" } ] }, "finishReason": "MAX_TOKENS" } ], "usageMetadata": { "promptTokenCount": 9, "candidatesTokenCount": 4000 } }""";

        await Assert.ThrowsAsync<AiOutputInvalidException>(() => Answering(HttpStatusCode.OK, body).GenerateJsonAsync(Request()));

        var log = Assert.Single(_logged);
        Assert.Equal(9, log.InputTokens);
        Assert.Equal(4000, log.OutputTokens);
    }

    [Fact]
    public async Task TheLogHoldsNoPromptNoAnswerAndNoWorkspace()
    {
        await Answering(HttpStatusCode.OK, Good).GenerateJsonAsync(Request());

        var properties = typeof(AiCallLog).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(properties, p => p.Contains("Prompt") || p.Contains("Json") || p.Contains("Workspace") || p.Contains("User"));
    }

    // ---------- the writer ----------

    [Fact]
    public async Task Writer_WhenTheDatabaseFails_SwallowsTheErrorSoTheCallIsNotFailed()
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(_ => throw new InvalidOperationException("database down"));
        var writer = new AiCallLogWriter(scopes, Substitute.For<ILogger<AiCallLogWriter>>());

        await writer.WriteAsync(new AiCallLog { Purpose = AiCallPurpose.BuildAnalysis, Model = Model });
    }
}
