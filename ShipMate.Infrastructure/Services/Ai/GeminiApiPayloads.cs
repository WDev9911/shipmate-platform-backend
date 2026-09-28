using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ShipMate.Infrastructure.Services.Ai;

internal class GeminiGenerateContentRequest
{
    [JsonPropertyName("systemInstruction")]
    public GeminiContent SystemInstruction { get; set; } = new();

    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig GenerationConfig { get; set; } = new();
}

internal class GeminiContent
{
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    // Set on parts that carry the model's internal reasoning rather than the answer.
    [JsonPropertyName("thought")]
    public bool Thought { get; set; }
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("responseMimeType")]
    public string ResponseMimeType { get; set; } = string.Empty;

    [JsonPropertyName("responseJsonSchema")]
    public JsonNode? ResponseJsonSchema { get; set; }
}

internal class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; set; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

internal class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }
}
