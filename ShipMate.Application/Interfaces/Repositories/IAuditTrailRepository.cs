using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IAuditTrailRepository
{
    /// <summary>The newest record of the workspace's chain, counting records added but not saved yet.</summary>
    Task<AuditTrailEntry?> GetLastAsync(Guid workspaceId);

    /// <summary>The saved chain, oldest first.</summary>
    Task<List<AuditTrailEntry>> GetChainAsync(Guid workspaceId);

    /// <summary>The saved records of one kind, oldest first.</summary>
    Task<List<AuditTrailEntry>> GetByTypeAsync(Guid workspaceId, string auditType);

    /// <summary>Queues a record; it is written by the next save of the unit of work.</summary>
    void Add(AuditTrailEntry entry);
}
