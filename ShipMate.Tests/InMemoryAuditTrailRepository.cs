using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Tests;

/// <summary>An audit chain kept in a list, so tests can read back what the services wrote.</summary>
internal sealed class InMemoryAuditTrailRepository : IAuditTrailRepository
{
    public List<AuditTrailEntry> Entries { get; } = new();

    public Task<AuditTrailEntry?> GetLastAsync(Guid workspaceId) =>
        Task.FromResult(Entries.Where(e => e.WorkspaceId == workspaceId).MaxBy(e => e.Sequence));

    public Task<List<AuditTrailEntry>> GetChainAsync(Guid workspaceId) =>
        Task.FromResult(Entries.Where(e => e.WorkspaceId == workspaceId).OrderBy(e => e.Sequence).ToList());

    public Task<List<AuditTrailEntry>> GetByTypeAsync(Guid workspaceId, string auditType) =>
        Task.FromResult(Entries.Where(e => e.WorkspaceId == workspaceId && e.AuditType == auditType).OrderBy(e => e.Sequence).ToList());

    public void Add(AuditTrailEntry entry) => Entries.Add(entry);
}
