using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>BUILD: developers hand code to ShipMate, which checks it against the locked scope before it reaches the feature branch.</summary>
public interface IBuildService
{
    /// <summary>
    /// The CLI reports a commit it pushed to the staging branch. Rejects submits that break the rules of the branch,
    /// the feature or the developer; otherwise records the submission for analysis.
    /// </summary>
    Task<BuildSubmissionDto> SubmitAsync(Guid userId, Guid workspaceId, SubmitBuildRequest request);

    /// <summary>A submission with its verdict, once analysed. Any active member of the workspace may look.</summary>
    Task<BuildSubmissionDto> GetSubmissionAsync(Guid userId, Guid workspaceId, Guid submissionId);
}
