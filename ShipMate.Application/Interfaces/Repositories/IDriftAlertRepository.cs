using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IDriftAlertRepository
{
    Task AddAsync(DriftAlert alert);
    Task<DriftAlert?> GetByIdAsync(Guid workspaceId, Guid alertId);

    /// <summary>The workspace's alerts, newest first, optionally only those in the given status.</summary>
    Task<List<DriftAlert>> GetByWorkspaceIdAsync(Guid workspaceId, DriftAlertStatus? status);

    /// <summary>Every alert of the workspace that still blocks approval (unresolved or acknowledged).</summary>
    Task<List<DriftAlert>> GetOpenByWorkspaceAsync(Guid workspaceId);

    /// <summary>Whether an alert raised against the given snapshot version is still open. VERIFY uses it to hold a release back.</summary>
    Task<bool> HasOpenDriftAsync(Guid workspaceId, int snapshotVersion);

    /// <summary>The most recently raised alert of the feature, in any state.</summary>
    Task<DriftAlert?> GetLatestByFeatureAsync(Guid workspaceId, Guid featureId);

    /// <summary>Alerts of the feature that still block approval (unresolved or acknowledged).</summary>
    Task<List<DriftAlert>> GetOpenByFeatureAsync(Guid workspaceId, Guid featureId);

    Task<bool> SaveChangesAsync();
}
