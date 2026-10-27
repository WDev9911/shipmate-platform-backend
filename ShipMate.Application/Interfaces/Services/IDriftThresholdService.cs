using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>BUILD: the Manager moves the drift threshold, within limits that keep it from being used to let drift through.</summary>
public interface IDriftThresholdService
{
    /// <summary>
    /// Sets the threshold. Only a Manager; the value must stay in range, differ from the current one, and the project
    /// must have changes left and not have changed it in the last 7 days. Every change is written to the audit trail,
    /// and a lowering is marked as a governance dilution.
    /// </summary>
    Task<DriftThresholdDto> ChangeAsync(Guid userId, Guid workspaceId, ChangeDriftThresholdRequest request);
}
