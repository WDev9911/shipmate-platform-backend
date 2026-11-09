namespace ShipMate.Application.Interfaces.Services;

/// <summary>The workspace's append-only, hash-chained history of events that matter to governance.</summary>
public interface IAuditTrail
{
    /// <summary>
    /// Adds a record to the end of the workspace's chain. Nothing is saved here: the record is written together with
    /// the change it describes, by that change's own save, so the two never part.
    /// </summary>
    Task AppendAsync(Guid workspaceId, string auditType, Guid? actorUserId, object payload);

    /// <summary>Re-computes the whole chain. Returns the sequence of the first record that doesn't hold up, or null when intact.</summary>
    Task<long?> FindFirstBrokenAsync(Guid workspaceId);
}
