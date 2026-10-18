namespace ShipMate.Application.Ai.Lock;

/// <summary>What is sent to the AI for LOCK step 1, serialized with <see cref="AiJson.Options"/>.</summary>
public class ComplexityScoringInput
{
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public List<ComplexityScoringFeatureInput> Features { get; set; } = new();
}

public class ComplexityScoringFeatureInput
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string? Role { get; set; }
}
