using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipMate.Application.Ai;

/// <summary>JSON conventions for everything exchanged with the AI: snake_case names and snake_case enum values.</summary>
public static class AiJson
{
    public static JsonNamingPolicy NamingPolicy { get; } = JsonNamingPolicy.SnakeCaseLower;

    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = NamingPolicy,
        Converters = { new JsonStringEnumConverter(NamingPolicy, allowIntegerValues: false) }
    };
}
