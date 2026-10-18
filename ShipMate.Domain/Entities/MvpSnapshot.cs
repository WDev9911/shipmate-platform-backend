using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

/// <summary>
/// The immutable, versioned MVP the team committed to at Lock. Features and dependencies are copied in full,
/// so a later version never depends on what the Product Definition looks like afterwards.
/// </summary>
public class MvpSnapshot : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public int Version { get; set; }

    // Hash of the sealed content, so any later tampering is detectable.
    public string Hash { get; set; } = string.Empty;

    // Active members counted once, at the moment of Lock.
    public int DevCountAtLock { get; set; }

    public Guid CreatedByUserId { get; set; }

    public ICollection<SnapshotFeature> Features { get; set; } = new List<SnapshotFeature>();
    public ICollection<SnapshotDependency> Dependencies { get; set; } = new List<SnapshotDependency>();
}
