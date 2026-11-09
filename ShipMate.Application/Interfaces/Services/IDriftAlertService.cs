using ShipMate.Application.DTOs.Build;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>BUILD: what happens to a drift alert after it is raised.</summary>
public interface IDriftAlertService
{
    /// <summary>Any active member may look. Newest first, optionally only the given status.</summary>
    Task<List<DriftAlertDto>> GetAllAsync(Guid userId, Guid workspaceId, DriftAlertStatus? status);

    /// <summary>The developer holding the feature confirms they saw the alert.</summary>
    Task<DriftAlertDto> AcknowledgeAsync(Guid userId, Guid workspaceId, Guid alertId);

    /// <summary>A Manager accepts the drift on purpose. The reason goes on the audit trail.</summary>
    Task<DriftAlertDto> OverrideAsync(Guid userId, Guid workspaceId, Guid alertId, OverrideDriftAlertRequest request);
}
