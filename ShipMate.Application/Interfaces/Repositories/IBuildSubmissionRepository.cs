using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

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

    /// <summary>The latest submission of the feature that got a verdict (in scope, drift, skipped or approved), except the given one.</summary>
    Task<BuildSubmission?> GetLatestVerdictAsync(Guid workspaceId, Guid featureId, Guid exceptSubmissionId);

    /// <summary>Reads the database fresh: false once a newer submit replaced this one.</summary>
    Task<bool> IsAnalyzingAsync(Guid id);

    Task<bool> SaveChangesAsync();
}
