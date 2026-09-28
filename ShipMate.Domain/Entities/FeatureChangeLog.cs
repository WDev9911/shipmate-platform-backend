using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// change_history of a committed feature (DEFINE step 8) — insert-only, never updated or deleted.
/// </summary>
public class FeatureChangeLog : BaseEntity
{
    public Guid FeatureId { get; set; }
    public Feature Feature { get; set; } = null!;

    public FeatureChangeAction Action { get; set; }

    // JSON snapshots of the feature content before and after the change.
    public string? OldContent { get; set; }
    public string? NewContent { get; set; }

    public string? Reason { get; set; }
    public bool CustomerNotifiedConfirmed { get; set; }

    public Guid PerformedByUserId { get; set; }
    public User PerformedBy { get; set; } = null!;
}
