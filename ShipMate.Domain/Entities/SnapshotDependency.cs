using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

/// <summary>One depends_on link of a snapshot. Every version stores all its links, not only what changed.</summary>
public class SnapshotDependency : BaseEntity
{
    public Guid SnapshotId { get; set; }
    public MvpSnapshot Snapshot { get; set; } = null!;

    public Guid FeatureId { get; set; }
    public Guid DependsOnFeatureId { get; set; }
}
