using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

/// <summary>
/// One link of a workspace's tamper-evident history. Each record carries the hash of the one before it, so changing
/// or removing any record breaks every hash after it. Records are only ever appended.
/// </summary>
public class AuditTrailEntry : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    // 1, 2, 3... within the workspace. Unique, so two writers can't both extend the chain from the same record.
    public long Sequence { get; set; }

    // One of AuditTypes.
    public string AuditType { get; set; } = string.Empty;

    // Null for events ShipMate raises itself.
    public Guid? ActorUserId { get; set; }

    // JSON, kept as the exact text that was hashed.
    public string Payload { get; set; } = "{}";

    public string PrevHash { get; set; } = string.Empty;
    public string RecordHash { get; set; } = string.Empty;
}
