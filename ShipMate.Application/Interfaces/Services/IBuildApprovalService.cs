using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>BUILD: the developer lets the Bot move the checked code onto the feature branch.</summary>
public interface IBuildApprovalService
{
    /// <summary>
    /// Moves the feature branch forward to the checked commit (the submission's head, or an earlier one of its
    /// commits) and clears the staging branch if nothing newer is on it. Only the developer holding the feature may
    /// approve, and only the newest submission, in scope or with its drift overridden by a Manager.
    /// </summary>
    Task<BuildSubmissionDto> ApproveAsync(Guid userId, Guid workspaceId, Guid submissionId, ApproveBuildRequest request);
}
