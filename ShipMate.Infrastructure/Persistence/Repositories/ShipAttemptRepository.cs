using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class ShipAttemptRepository : IShipAttemptRepository
{
    private readonly AppDbContext _context;

    public ShipAttemptRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ShipAttempt attempt) =>
        await _context.ShipAttempts.AddAsync(attempt);

    public async Task<ShipAttempt?> GetByIdAsync(Guid workspaceId, Guid attemptId) =>
        await _context.ShipAttempts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.WorkspaceId == workspaceId && a.Id == attemptId);

    public async Task<List<ShipAttempt>> GetRecentAsync(Guid workspaceId, int limit) =>
        await _context.ShipAttempts.AsNoTracking()
            .Where(a => a.WorkspaceId == workspaceId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<List<DateTime>> GetStartTimesSinceAsync(Guid workspaceId, DateTime since) =>
        await _context.ShipAttempts
            .Where(a => a.WorkspaceId == workspaceId && a.CreatedAt > since)
            .Select(a => a.CreatedAt)
            .ToListAsync();

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
