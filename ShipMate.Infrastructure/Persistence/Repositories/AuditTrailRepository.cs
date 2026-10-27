using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class AuditTrailRepository : IAuditTrailRepository
{
    private readonly AppDbContext _context;

    public AuditTrailRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AuditTrailEntry?> GetLastAsync(Guid workspaceId)
    {
        // Several records can be appended before one save (e.g. a bypass and its solo note): they chain onto each other.
        var pending = _context.AuditTrail.Local
            .Where(e => e.WorkspaceId == workspaceId)
            .MaxBy(e => e.Sequence);
        if (pending is not null)
        {
            return pending;
        }

        return await _context.AuditTrail.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId)
            .OrderByDescending(e => e.Sequence)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AuditTrailEntry>> GetChainAsync(Guid workspaceId) =>
        await _context.AuditTrail.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId)
            .OrderBy(e => e.Sequence)
            .ToListAsync();

    public async Task<List<AuditTrailEntry>> GetByTypeAsync(Guid workspaceId, string auditType) =>
        await _context.AuditTrail.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId && e.AuditType == auditType)
            .OrderBy(e => e.Sequence)
            .ToListAsync();

    public void Add(AuditTrailEntry entry) =>
        _context.AuditTrail.Add(entry);
}
