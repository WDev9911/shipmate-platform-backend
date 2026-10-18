using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>LOCK: check the three gates, then seal the product definition into an immutable MVP snapshot.</summary>
public interface ILockService
{
    /// <summary>Runs the gate checks without locking, so the Manager can see what still blocks.</summary>
    Task<LockGateDto> CheckAsync(Guid userId, Guid workspaceId);

    Task<MvpSnapshotDto> LockAsync(Guid userId, Guid workspaceId);

    Task<MvpSnapshotDto> GetLatestSnapshotAsync(Guid userId, Guid workspaceId);
}
