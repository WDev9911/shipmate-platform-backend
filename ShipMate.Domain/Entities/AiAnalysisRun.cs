using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

public class AiAnalysisRun : BaseEntity
{
    public Guid ProductDefinitionId { get; set; }
    public ProductDefinition ProductDefinition { get; set; } = null!;

    public Guid RunByUserId { get; set; }
    public User RunBy { get; set; } = null!;

    public string VisionPromptSnapshot { get; set; } = string.Empty;
    // JSON of the constraints the AI was given; null when there were none.
    public string? ConstraintsSnapshot { get; set; }
    public string Model { get; set; } = string.Empty;

    public bool Succeeded { get; set; }
    public string? RawResponse { get; set; }
    public string? ErrorMessage { get; set; }
}
