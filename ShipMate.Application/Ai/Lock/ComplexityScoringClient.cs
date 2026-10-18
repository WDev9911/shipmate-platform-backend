using System.Text.Json;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Ai.Lock;

/// <summary>Asks the AI to score features 1-5 and returns only a result that passed validation.</summary>
public static class ComplexityScoringClient
{
    private static readonly Lazy<string> SystemInstruction =
        new(() => AiResources.ReadText(LockPrompts.ComplexitySystemInstructionFile));

    private static readonly Lazy<string> OutputSchema =
        new(() => AiResources.ReadText(LockPrompts.ComplexitySchemaFile));

    public static async Task<ComplexityScoringOutput> ScoreAsync(
        IAiGateway aiGateway, ComplexityScoringInput input, CancellationToken cancellationToken)
    {
        var response = await aiGateway.GenerateJsonAsync(new AiJsonRequest
        {
            SystemInstruction = SystemInstruction.Value,
            UserPrompt = JsonSerializer.Serialize(input, AiJson.Options),
            JsonSchema = OutputSchema.Value
        }, cancellationToken);

        ComplexityScoringOutput? output;
        try
        {
            output = JsonSerializer.Deserialize<ComplexityScoringOutput>(response.Json, AiJson.Options);
        }
        catch (JsonException)
        {
            throw new AiOutputInvalidException(response.Json);
        }

        var featureIds = input.Features.Select(f => f.Id).ToList();
        if (output is null || ComplexityScoringOutputValidator.Validate(output, featureIds).Count > 0)
        {
            throw new AiOutputInvalidException(response.Json);
        }

        return output;
    }
}
