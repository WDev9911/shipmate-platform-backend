using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class ShipCardRepository : IShipCardRepository
{
    private readonly AppDbContext _context;

    public ShipCardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ShipCard card) =>
        await _context.ShipCards.AddAsync(card);

    public async Task<ShipCard?> GetByIdAsync(Guid workspaceId, Guid cardId) =>
        await _context.ShipCards.FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Id == cardId);

    public async Task<ShipCard?> GetByVersionAsync(Guid workspaceId, int snapshotVersion) =>
        await _context.ShipCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.SnapshotVersion == snapshotVersion);

    public async Task<ShipCard?> GetBySlugAsync(string publicSlug) =>
        await _context.ShipCards.AsNoTracking().FirstOrDefaultAsync(c => c.PublicSlug == publicSlug);

    public async Task<List<ShipCard>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        await _context.ShipCards.AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderByDescending(c => c.SnapshotVersion)
            .ToListAsync();

    public async Task<bool> ExistsAsync(Guid workspaceId, int snapshotVersion) =>
        await _context.ShipCards.AnyAsync(c => c.WorkspaceId == workspaceId && c.SnapshotVersion == snapshotVersion);

    public async Task<bool> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync() > 0;
        }
        catch (DbUpdateException ex) when (UniqueViolation.Matches(ex, UniqueViolation.ShipCardVersionIndex))
        {
            throw new ShipAlreadyShippedException();
        }
    }
}
