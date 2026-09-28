namespace ShipMate.Application.DTOs.Ai;

public class AiJsonRequest
{
    public string SystemInstruction { get; init; } = string.Empty;
    public string UserPrompt { get; init; } = string.Empty;

    // A standard JSON Schema the provider must constrain its output to.
    public string JsonSchema { get; init; } = string.Empty;
}
