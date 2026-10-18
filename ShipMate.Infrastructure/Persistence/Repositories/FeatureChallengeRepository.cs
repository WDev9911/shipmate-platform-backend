using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class FeatureChallengeRepository : IFeatureChallengeRepository
{
    private readonly AppDbContext _context;

    public FeatureChallengeRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FeatureChallenge?> GetByIdAsync(Guid workspaceId, Guid challengeId) =>
        await _context.FeatureChallenges
            .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Id == challengeId);

    public async Task<List<FeatureChallenge>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        await _context.FeatureChallenges
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(FeatureChallenge challenge) =>
        await _context.FeatureChallenges.AddAsync(challenge);

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
