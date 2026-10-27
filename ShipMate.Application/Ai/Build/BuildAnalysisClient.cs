using System.Text.Json;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Ai.Build;

/// <summary>What the AI said, with the raw text and model kept for tracing.</summary>
public record BuildAnalysisResult(BuildAnalysisOutput Output, string RawJson, string Model);

/// <summary>One call that checks the code against the locked scope and looks for ways to simplify it.</summary>
public static class BuildAnalysisClient
{
    private static readonly Lazy<string> SystemInstruction =
        new(() => AiResources.ReadText(BuildPrompts.SystemInstructionFile));

    private static readonly Lazy<string> OutputSchema =
        new(() => AiResources.ReadText(BuildPrompts.SchemaFile));

    /// <summary>Returns only a result that passed validation; otherwise throws AiOutputInvalidException with the raw text.</summary>
    public static async Task<BuildAnalysisResult> AnalyzeAsync(
        IAiGateway aiGateway, BuildAnalysisInput input, CancellationToken cancellationToken)
    {
        var response = await aiGateway.GenerateJsonAsync(new AiJsonRequest
        {
            SystemInstruction = SystemInstruction.Value,
            UserPrompt = JsonSerializer.Serialize(input, AiJson.Options),
            JsonSchema = OutputSchema.Value
        }, cancellationToken);

        BuildAnalysisOutput? output;
        try
        {
            output = JsonSerializer.Deserialize<BuildAnalysisOutput>(response.Json, AiJson.Options);
        }
        catch (JsonException)
        {
            throw new AiOutputInvalidException(response.Json);
        }

        var errors = output is null
            ? ["The AI returned an empty result."]
            : BuildAnalysisOutputValidator.Validate(output, input.OtherFeatures.Select(f => f.Id).ToList());
        if (errors.Count > 0)
        {
            throw new AiOutputInvalidException(response.Json);
        }

        return new BuildAnalysisResult(output!, response.Json, response.Model);
    }
}
