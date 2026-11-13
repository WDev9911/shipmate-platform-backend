using ShipMate.Application.DTOs.Verify;

namespace ShipMate.Application.Interfaces.Services;

public interface IVerificationService
{
    /// <summary>The workspace's checklists, newest version first.</summary>
    Task<List<VerificationChecklistSummaryDto>> GetAllAsync(Guid userId, Guid workspaceId);

    Task<VerificationChecklistDto> GetAsync(Guid userId, Guid workspaceId, int version);

    Task<VerificationChecklistDto> CheckItemAsync(
        Guid userId, Guid workspaceId, int version, string itemKey, CheckVerificationItemRequest request);

    /// <summary>Manager only: skip a check that doesn't fit the product, or bring it back.</summary>
    Task<VerificationChecklistDto> SetApplicabilityAsync(
        Guid userId, Guid workspaceId, int version, string itemKey, SetVerificationItemApplicabilityRequest request);

    /// <summary>
    /// For SHIP and NBA. It checks nobody's access: the caller has already done that.
    /// A version without a checklist comes back as <see cref="VerificationReadiness.Missing"/>.
    /// </summary>
    Task<VerificationReadiness> GetReadinessAsync(Guid workspaceId, int version);
}
