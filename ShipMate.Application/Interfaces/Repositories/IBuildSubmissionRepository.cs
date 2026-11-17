using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

/// <summary>An approved submission, without its analysis. <c>ApprovedByName</c> is null if the account is gone.</summary>
public record ApprovedSubmissionInfo(
    Guid FeatureId,
    Guid SubmissionId,
    DateTime ApprovedAt,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    string? ApprovedSha);

public interface IBuildSubmissionRepository
{
    Task AddAsync(BuildSubmission submission);
    Task<BuildSubmission?> GetByIdAsync(Guid id);

    /// <summary>Submissions of the feature still waiting for or going through analysis.</summary>
    Task<List<BuildSubmission>> GetAnalyzingAsync(Guid workspaceId, Guid featureId);

    /// <summary>Every submission still waiting for analysis, used to pick work back up after a restart.</summary>
    Task<List<BuildSubmission>> GetAllAnalyzingAsync();

    /// <summary>The newest submission of the feature in any state.</summary>
    Task<BuildSubmission?> GetLatestAsync(Guid workspaceId, Guid featureId);

    /// <summary>The newest submission of every feature of the workspace that has one, in a single query.</summary>
    Task<List<BuildSubmission>> GetLatestPerFeatureAsync(Guid workspaceId);

    /// <summary>The most recent approval of every feature of the workspace that was ever approved, in a single query.</summary>
    Task<List<ApprovedSubmissionInfo>> GetLatestApprovalPerFeatureAsync(Guid workspaceId);

    /// <summary>The latest submission of the feature that got a verdict (in scope, drift, skipped or approved), except the given one.</summary>
    Task<BuildSubmission?> GetLatestVerdictAsync(Guid workspaceId, Guid featureId, Guid exceptSubmissionId);

    /// <summary>Reads the database fresh: false once a newer submit replaced this one.</summary>
    Task<bool> IsAnalyzingAsync(Guid id);

    Task<bool> SaveChangesAsync();
}
