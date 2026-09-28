using ShipMate.Domain.Enums;

namespace ShipMate.Application.Ai.Define;

/// <summary>What the AI returns for DEFINE, matching define-analysis.schema.json.</summary>
public class DefineAnalysisOutput
{
    public DefinePersonaOutput Persona { get; set; } = new();
    public DefineProblemSolutionOutput ProblemSolution { get; set; } = new();
    public List<DefineFeatureOutput> Features { get; set; } = new();
}

public class DefinePersonaOutput
{
    public string PrimaryPersona { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<DefineSupportingRoleOutput> SupportingRoles { get; set; } = new();
}

public class DefineSupportingRoleOutput
{
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class DefineProblemSolutionOutput
{
    public string Problem { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
}

public class DefineFeatureOutput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public FeatureRole Role { get; set; }
    public FeatureOrigin Origin { get; set; }
    public DefineAiAssessmentOutput AiAssessment { get; set; } = new();
    public bool PossibleDuplicate { get; set; }
    public string? DuplicateOf { get; set; }
    public string? DuplicateReason { get; set; }
    public bool PersonaConflict { get; set; }
    public string? PersonaConflictReason { get; set; }
    public List<DefineDependencyOutput> DependsOn { get; set; } = new();
}

public class DefineAiAssessmentOutput
{
    public AiVerdict Verdict { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DefineDependencyOutput
{
    public string FeatureId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
