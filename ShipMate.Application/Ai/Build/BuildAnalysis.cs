namespace ShipMate.Application.Ai.Build;

public static class BuildPrompts
{
    public const string SystemInstructionFile = "build-analysis.system.md";
    public const string SchemaFile = "build-analysis.schema.json";
}

public class BuildAnalysisInput
{
    public BuildFeatureInput Feature { get; set; } = new();
    public List<BuildOtherFeatureInput> OtherFeatures { get; set; } = new();
    public List<BuildDependencyInput> Dependencies { get; set; } = new();
    public List<BuildDiffFileInput> Diff { get; set; } = new();
}

public class BuildFeatureInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
}

public class BuildOtherFeatureInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    // locked, backup or replaced, as in the snapshot
    public string Status { get; set; } = string.Empty;
}

public class BuildDependencyInput
{
    public string FeatureId { get; set; } = string.Empty;
    public string DependsOnFeatureId { get; set; } = string.Empty;
}

public class BuildDiffFileInput
{
    public string File { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Patch { get; set; }
}

public class BuildAnalysisOutput
{
    public decimal ConfidenceScore { get; set; }
    public List<string> SuspectedFeatureIds { get; set; } = new();
    public List<BuildFinding> Findings { get; set; } = new();
    public List<BuildSuggestion> Suggestions { get; set; } = new();
}

public record BuildFinding(string File, string Description);

public record BuildSuggestion(string File, string Problem, string Suggestion, string Reason);
