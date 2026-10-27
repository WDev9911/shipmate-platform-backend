using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class BuildSubmissionRepository : IBuildSubmissionRepository
{
    private readonly AppDbContext _context;

    public BuildSubmissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BuildSubmission submission) =>
        await _context.BuildSubmissions.AddAsync(submission);

    public async Task<BuildSubmission?> GetByIdAsync(Guid id) =>
        await _context.BuildSubmissions.FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<BuildSubmission>> GetAnalyzingAsync(Guid workspaceId, Guid featureId) =>
        await _context.BuildSubmissions
            .Where(s => s.WorkspaceId == workspaceId && s.FeatureId == featureId
                && s.Status == BuildSubmissionStatus.Analyzing)
            .ToListAsync();

    public async Task<List<BuildSubmission>> GetAllAnalyzingAsync() =>
        await _context.BuildSubmissions
            .Where(s => s.Status == BuildSubmissionStatus.Analyzing)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

    public async Task<BuildSubmission?> GetLatestAsync(Guid workspaceId, Guid featureId) =>
        await _context.BuildSubmissions
            .Where(s => s.WorkspaceId == workspaceId && s.FeatureId == featureId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<BuildSubmission?> GetLatestVerdictAsync(Guid workspaceId, Guid featureId, Guid exceptSubmissionId) =>
        await _context.BuildSubmissions
            .Where(s => s.WorkspaceId == workspaceId && s.FeatureId == featureId && s.Id != exceptSubmissionId
                && (s.Status == BuildSubmissionStatus.InScope
                    || s.Status == BuildSubmissionStatus.Drift
                    || s.Status == BuildSubmissionStatus.SkippedMaintenance
                    || s.Status == BuildSubmissionStatus.Approved))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<bool> IsAnalyzingAsync(Guid id) =>
        await _context.BuildSubmissions.AsNoTracking()
            .AnyAsync(s => s.Id == id && s.Status == BuildSubmissionStatus.Analyzing);

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
