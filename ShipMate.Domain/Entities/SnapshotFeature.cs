using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

public class SnapshotFeature : BaseEntity
{
    public Guid SnapshotId { get; set; }
    public MvpSnapshot Snapshot { get; set; } = null!;

    // The Feature this was copied from. Not a foreign key: the snapshot stays valid whatever happens to it.
    public Guid FeatureId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public FeatureRole? Role { get; set; }
    public FeatureOrigin Origin { get; set; }
    public int Complexity { get; set; }
    public SnapshotFeatureStatus Status { get; set; } = SnapshotFeatureStatus.Locked;
}
