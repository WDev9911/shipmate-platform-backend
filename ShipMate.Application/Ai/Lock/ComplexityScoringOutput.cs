namespace ShipMate.Application.Ai.Lock;

/// <summary>What the AI returns for LOCK step 1, matching complexity-scoring.schema.json.</summary>
public class ComplexityScoringOutput
{
    public List<ComplexityScoreOutput> Scores { get; set; } = new();
}

public class ComplexityScoreOutput
{
    public string FeatureId { get; set; } = string.Empty;
    public int Complexity { get; set; }
    public string Reason { get; set; } = string.Empty;
}
