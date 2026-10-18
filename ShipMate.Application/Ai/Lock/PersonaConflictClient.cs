using System.Text.Json;
using ShipMate.Application.Ai.Define;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Ai.Lock;

public class PersonaConflictInput
{
    public DefinePersona Persona { get; set; } = new();
    public PersonaConflictFeatureInput Feature { get; set; } = new();
}

public class PersonaConflictFeatureInput
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
}

public class PersonaConflictOutput
{
    public bool PersonaConflict { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// The Persona Conflict Check shared by DEFINE and LOCK, as a standalone call: one feature against the
/// locked persona. Returns only a result that passed validation.
/// </summary>
public static class PersonaConflictClient
{
    private static readonly Lazy<string> SystemInstruction =
        new(() => AiResources.ReadText(LockPrompts.PersonaConflictSystemInstructionFile));

    private static readonly Lazy<string> OutputSchema =
        new(() => AiResources.ReadText(LockPrompts.PersonaConflictSchemaFile));

    public static async Task<PersonaConflictOutput> CheckAsync(
        IAiGateway aiGateway, PersonaConflictInput input, CancellationToken cancellationToken)
    {
        var response = await aiGateway.GenerateJsonAsync(new AiJsonRequest
        {
            SystemInstruction = SystemInstruction.Value,
            UserPrompt = JsonSerializer.Serialize(input, AiJson.Options),
            JsonSchema = OutputSchema.Value
        }, cancellationToken);

        PersonaConflictOutput? output;
        try
        {
            output = JsonSerializer.Deserialize<PersonaConflictOutput>(response.Json, AiJson.Options);
        }
        catch (JsonException)
        {
            throw new AiOutputInvalidException(response.Json);
        }

        if (output is null || string.IsNullOrWhiteSpace(output.Reason))
        {
            throw new AiOutputInvalidException(response.Json);
        }

        return output;
    }
}
