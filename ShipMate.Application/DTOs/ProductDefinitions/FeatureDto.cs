using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class FeatureDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public FeatureRole? Role { get; set; }
    public FeatureOrigin Origin { get; set; }
    public List<FeatureOrigin> Sources { get; set; } = new();
    public AiAssessmentDto? AiAssessment { get; set; }
    public FeatureStatus Status { get; set; }
    public bool PossibleDuplicate { get; set; }
    public Guid? DuplicateOfId { get; set; }
    public string? DuplicateReason { get; set; }
    public bool PersonaConflict { get; set; }
    public string? PersonaConflictReason { get; set; }
    public bool DevDecided { get; set; }
    public int Position { get; set; }
    public List<FeatureDependencyDto> DependsOn { get; set; } = new();
}
