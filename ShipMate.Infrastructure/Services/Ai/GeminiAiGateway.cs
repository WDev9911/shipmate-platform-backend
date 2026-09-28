using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;

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
    private readonly ILogger<GeminiAiGateway> _logger;

    public GeminiAiGateway(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiAiGateway> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Model => _options.Model;

    public async Task<AiJsonResponse> GenerateJsonAsync(AiJsonRequest request, CancellationToken cancellationToken = default)
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
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

            var result = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken);
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
