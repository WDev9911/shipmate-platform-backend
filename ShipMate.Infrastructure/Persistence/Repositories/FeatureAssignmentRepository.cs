using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class FeatureAssignmentRepository : IFeatureAssignmentRepository
{
    private readonly AppDbContext _context;

    public FeatureAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<FeatureAssignment>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        await _context.FeatureAssignments
            .Include(a => a.AssignedUser)
            .Where(a => a.WorkspaceId == workspaceId)
            .ToListAsync();

    public async Task<bool> TryAddAsync(FeatureAssignment assignment)
    {
        await _context.FeatureAssignments.AddAsync(assignment);
        try
        {
            await _context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (UniqueViolation.Matches(ex))
        {
            _context.Entry(assignment).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
