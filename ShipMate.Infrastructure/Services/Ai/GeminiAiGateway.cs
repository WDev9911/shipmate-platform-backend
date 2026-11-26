using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Services.Ai;

public class GeminiAiGateway : IAiGateway
{
    // Gemini REST API protocol values.
    private const string JsonMimeType = "application/json";
    private const string UserRole = "user";
    private const string CompletedFinishReason = "STOP";
    private const string ApiKeyHeader = "x-goog-api-key";

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly IAiCallLogWriter _callLog;
    private readonly ILogger<GeminiAiGateway> _logger;

    public GeminiAiGateway(
        HttpClient httpClient, IOptions<GeminiOptions> options, IAiCallLogWriter callLog, ILogger<GeminiAiGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _callLog = callLog;
        _logger = logger;
    }

    public string Model => _options.Model;

    public async Task<AiJsonResponse> GenerateJsonAsync(AiJsonRequest request, CancellationToken cancellationToken = default)
    {
        // Every way out of this method leaves one line in the call log: the outcome is set before each exit.
        var log = new AiCallLog { Purpose = request.Purpose, Model = _options.Model, Outcome = AiCallOutcome.ProviderUnavailable };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await CallAsync(request, log, cancellationToken);
            log.Outcome = AiCallOutcome.Succeeded;
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shut down or replaced by a newer submit: not the provider's fault. Let it through as a cancellation, so
            // BUILD keeps the submission Analyzing and runs it again after a restart.
            log.Outcome = AiCallOutcome.Cancelled;
            throw;
        }
        catch (AiRateLimitedException)
        {
            log.Outcome = AiCallOutcome.RateLimited;
            throw;
        }
        catch (AiOutputInvalidException)
        {
            log.Outcome = AiCallOutcome.InvalidOutput;
            throw;
        }
        finally
        {
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            await _callLog.WriteAsync(log);
        }
    }

    private async Task<AiJsonResponse> CallAsync(AiJsonRequest request, AiCallLog log, CancellationToken cancellationToken)
    {
        var payload = new GeminiGenerateContentRequest
        {
            SystemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = request.SystemInstruction }] },
            Contents = [new GeminiContent { Role = UserRole, Parts = [new GeminiPart { Text = request.UserPrompt }] }],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = JsonMimeType,
                ResponseJsonSchema = JsonNode.Parse(request.JsonSchema)
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"models/{_options.Model}:generateContent")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Add(ApiKeyHeader, _options.ApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        // A cancellation asked for by the caller is not an outage; only a timeout or a network error is.
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Gemini request failed before a response was received.");
            throw new AiProviderUnavailableException();
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Gemini rate limit reached for model {Model}.", _options.Model);
                throw new AiRateLimitedException();
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini returned {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
                throw new AiProviderUnavailableException();
            }

            GeminiGenerateContentResponse? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken);
            }
            catch (JsonException ex)
            {
                // A body that isn't the documented JSON is the provider failing, like an error status.
                _logger.LogError(ex, "Gemini returned a body that could not be read.");
                throw new AiProviderUnavailableException();
            }

            log.Model = result?.ModelVersion ?? _options.Model;
            log.InputTokens = result?.UsageMetadata?.PromptTokenCount;
            log.OutputTokens = result?.UsageMetadata?.CandidatesTokenCount;
            log.ThinkingTokens = result?.UsageMetadata?.ThoughtsTokenCount;

            var candidate = result?.Candidates?.FirstOrDefault();
            var json = string.Concat(candidate?.Content?.Parts
                .Where(part => !part.Thought)
                .Select(part => part.Text) ?? []);

            // Anything other than a normal finish (e.g. hit the token limit, blocked by safety filters)
            // means the JSON is missing or cut off.
            if (candidate?.FinishReason != CompletedFinishReason || string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning(
                    "Gemini returned no usable output. FinishReason: {FinishReason}, BlockReason: {BlockReason}",
                    candidate?.FinishReason, result?.PromptFeedback?.BlockReason);
                throw new AiOutputInvalidException(json);
            }

            return new AiJsonResponse
            {
                Json = json,
                Model = result!.ModelVersion ?? _options.Model
            };
        }
    }
}
