using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>One `shipmate submit`: the developer pushed code to the staging branch and asked ShipMate to check it.</summary>
public class BuildSubmission : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid FeatureId { get; set; }

    public Guid SubmittedByUserId { get; set; }
    public User SubmittedByUser { get; set; } = null!;

    public string StagingBranch { get; set; } = string.Empty;
    public string HeadSha { get; set; } = string.Empty;

    // The snapshot the code was checked against.
    public int SnapshotVersion { get; set; }

    public BuildSubmissionStatus Status { get; set; } = BuildSubmissionStatus.Analyzing;

    // The verdict. Null until the analysis finishes; copied from the earlier submission when the AI was skipped.
    public decimal? ConfidenceScore { get; set; }
    public decimal? ThresholdAtCheck { get; set; }
    public List<Guid> SuspectedFeatureIds { get; set; } = new();
    // JSON arrays: [{ file, description }] and [{ file, problem, suggestion, reason }]
    public string? FindingsJson { get; set; }
    public string? SuggestionsJson { get; set; }
    public Guid? CarriedFromSubmissionId { get; set; }
    public DateTime? AnalyzedAt { get; set; }

    // For a Failed submission, e.g. DIFF_TOO_LARGE or AI_RATE_LIMITED.
    public string? FailureCode { get; set; }
    public string? AiModel { get; set; }
    public string? AiRawResponse { get; set; }

    // Set when the developer approved: the commit the Bot moved the feature branch to (the head, or an earlier one picked).
    public string? ApprovedSha { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
