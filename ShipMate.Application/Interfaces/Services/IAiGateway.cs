using ShipMate.Application.DTOs.Ai;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// Provider-agnostic AI entry point: send a prompt plus a JSON Schema, get back JSON that matches it.
/// Swapping providers (Gemini, Claude, OpenAI...) only means a new implementation of this interface.
/// </summary>
public interface IAiGateway
{
    // The configured model, known even when a call fails before the provider reports its exact version.
    string Model { get; }

    Task<AiJsonResponse> GenerateJsonAsync(AiJsonRequest request, CancellationToken cancellationToken = default);
}
