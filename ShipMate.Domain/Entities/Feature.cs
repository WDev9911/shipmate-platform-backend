using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

public class Feature : BaseEntity
{
    public Guid ProductDefinitionId { get; set; }
    public ProductDefinition ProductDefinition { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }

    // Role and AI assessment stay null for a committed feature entered before the first AI analysis.
    public FeatureRole? Role { get; set; }

    public FeatureOrigin Origin { get; set; }
    public List<FeatureOrigin> Sources { get; set; } = new();

    public AiVerdict? AiVerdict { get; set; }
    public string? AiReason { get; set; }

    public FeatureStatus Status { get; set; }

    public bool PossibleDuplicate { get; set; }
    public Guid? DuplicateOfId { get; set; }
    public Feature? DuplicateOf { get; set; }
    public string? DuplicateReason { get; set; }

    public bool PersonaConflict { get; set; }
    public string? PersonaConflictReason { get; set; }

    public bool DevDecided { get; set; }

    // Display order; also breaks ties when merging two features of equal origin priority.
    public int Position { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<FeatureDependency> Dependencies { get; set; } = new List<FeatureDependency>();
    public ICollection<FeatureChangeLog> ChangeLogs { get; set; } = new List<FeatureChangeLog>();
}
