namespace ShipMate.Application.DTOs.Ai;

public class AiJsonResponse
{
    public string Json { get; init; } = string.Empty;

    // The exact model version that produced the output, as reported by the provider.
    public string Model { get; init; } = string.Empty;
}
