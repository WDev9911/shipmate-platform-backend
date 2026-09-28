using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Domain.Entities;

public class ProductDefinition : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public ProductDefinitionStatus Status { get; set; } = ProductDefinitionStatus.Draft;

    // Null until the first successful AI analysis.
    public LockedPersona? LockedPersona { get; set; }
    public string? Problem { get; set; }
    public string? Solution { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Feature> Features { get; set; } = new List<Feature>();
    public ICollection<AiAnalysisRun> AnalysisRuns { get; set; } = new List<AiAnalysisRun>();

    public int NextFeaturePosition() => Features.Count == 0 ? 0 : Features.Max(f => f.Position) + 1;

    public void MarkReadyForLock(DateTime now)
    {
        Status = ProductDefinitionStatus.ReadyForLock;
        UpdatedAt = now;
    }

    // READY_FOR_LOCK is the developer's statement that this exact content is final, so any change
    // sends it back to Draft until they confirm again.
    public void MarkModified(DateTime now)
    {
        UpdatedAt = now;
        if (Status == ProductDefinitionStatus.ReadyForLock)
        {
            Status = ProductDefinitionStatus.Draft;
        }
    }
}
