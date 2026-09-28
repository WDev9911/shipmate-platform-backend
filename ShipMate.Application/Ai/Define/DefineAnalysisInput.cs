using ShipMate.Domain.Enums;

namespace ShipMate.Application.Ai.Define;

/// <summary>What DEFINE sends to the AI (spec "Input" schema), serialized with <see cref="AiJson.Options"/>.</summary>
public class DefineAnalysisInput
{
    public string VisionPrompt { get; set; } = string.Empty;
    public List<DefineCommittedFeatureInput> CommittedFeatures { get; set; } = new();

    // Re-analysis only (DEFINE step 5b); empty / null on the first analysis.
    public List<DefineExistingFeatureInput> ExistingFeatures { get; set; } = new();
    public DefinePersona? CurrentPersona { get; set; }
    public string? Instruction { get; set; }
    public bool ReassessPersona { get; set; }
}

public class DefineCommittedFeatureInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>A feature as it currently stands, so the AI knows what it must keep on a re-analysis.</summary>
public class DefineExistingFeatureInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public FeatureRole? Role { get; set; }
    public FeatureOrigin Origin { get; set; }
    public FeatureStatus Status { get; set; }
    public AiVerdict? AiVerdict { get; set; }

    // False for everything the developer decided on or that came from their input; the AI must keep those.
    public bool CanRegenerate { get; set; }

    public List<DefineDependencyOutput> DependsOn { get; set; } = new();
}
