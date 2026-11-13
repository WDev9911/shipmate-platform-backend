using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class VerificationChecklistRepository : IVerificationChecklistRepository
{
    private readonly AppDbContext _context;

    public VerificationChecklistRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(VerificationChecklist checklist) =>
        await _context.VerificationChecklists.AddAsync(checklist);

    public async Task<VerificationChecklist?> GetByVersionAsync(Guid workspaceId, int snapshotVersion) =>
        await _context.VerificationChecklists
            .Include(c => c.Items)
            .Include(c => c.MvpSnapshot).ThenInclude(s => s.Features)
            .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.SnapshotVersion == snapshotVersion);

    public async Task<List<VerificationChecklist>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        await _context.VerificationChecklists
            .Include(c => c.Items)
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderByDescending(c => c.SnapshotVersion)
            .ToListAsync();

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
