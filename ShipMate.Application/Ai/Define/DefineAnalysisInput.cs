namespace ShipMate.Application.Ai.Define;

/// <summary>What DEFINE sends to the AI (spec "Input" schema), serialized with <see cref="AiJson.Options"/>.</summary>
public class DefineAnalysisInput
{
    public string VisionPrompt { get; set; } = string.Empty;
    public List<DefineCommittedFeatureInput> CommittedFeatures { get; set; } = new();
}

public class DefineCommittedFeatureInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
