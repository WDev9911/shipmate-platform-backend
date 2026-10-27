using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class DriftAlertRepository : IDriftAlertRepository
{
    private readonly AppDbContext _context;

    public DriftAlertRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DriftAlert alert) =>
        await _context.DriftAlerts.AddAsync(alert);

    public async Task<DriftAlert?> GetByIdAsync(Guid workspaceId, Guid alertId) =>
        await _context.DriftAlerts.FirstOrDefaultAsync(a => a.WorkspaceId == workspaceId && a.Id == alertId);

    public async Task<List<DriftAlert>> GetByWorkspaceIdAsync(Guid workspaceId, DriftAlertStatus? status) =>
        await _context.DriftAlerts
            .Where(a => a.WorkspaceId == workspaceId && (status == null || a.Status == status))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public async Task<List<DriftAlert>> GetOpenByWorkspaceAsync(Guid workspaceId) =>
        await _context.DriftAlerts
            .Where(a => a.WorkspaceId == workspaceId
                && (a.Status == DriftAlertStatus.Unresolved || a.Status == DriftAlertStatus.Acknowledged))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

    public async Task<bool> HasOpenDriftAsync(Guid workspaceId, int snapshotVersion) =>
        await _context.DriftAlerts.AnyAsync(a => a.WorkspaceId == workspaceId
            && a.SnapshotVersionAtCheck == snapshotVersion
            && (a.Status == DriftAlertStatus.Unresolved || a.Status == DriftAlertStatus.Acknowledged));

    public async Task<DriftAlert?> GetLatestByFeatureAsync(Guid workspaceId, Guid featureId) =>
        await _context.DriftAlerts
            .Where(a => a.WorkspaceId == workspaceId && a.FeatureId == featureId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<List<DriftAlert>> GetOpenByFeatureAsync(Guid workspaceId, Guid featureId) =>
        await _context.DriftAlerts
            .Where(a => a.WorkspaceId == workspaceId && a.FeatureId == featureId
                && (a.Status == DriftAlertStatus.Unresolved || a.Status == DriftAlertStatus.Acknowledged))
            .ToListAsync();

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
