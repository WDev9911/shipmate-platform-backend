using System.Text.Json;
using ShipMate.Application.Ai;
using ShipMate.Application.Ai.Build;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Build;

public class BuildSubmissionDto
{
    public Guid Id { get; set; }
    public Guid FeatureId { get; set; }
    public string StagingBranch { get; set; } = string.Empty;
    public string HeadSha { get; set; } = string.Empty;
    public int SnapshotVersion { get; set; }
    public BuildSubmissionStatus Status { get; set; }

    // The verdict, once the analysis has finished.
    public decimal? ConfidenceScore { get; set; }
    public decimal? ThresholdAtCheck { get; set; }
    public List<Guid> SuspectedFeatureIds { get; set; } = new();
    public List<BuildFinding> Findings { get; set; } = new();
    public List<BuildSuggestion> Suggestions { get; set; } = new();

    // Set when the AI was skipped: the submission whose verdict this one repeats.
    public Guid? CarriedFromSubmissionId { get; set; }

    // Set when Status is Failed, e.g. DIFF_TOO_LARGE.
    public string? FailureCode { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? AnalyzedAt { get; set; }

    // Set once approved: the commit the feature branch was moved to.
    public string? ApprovedSha { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public static BuildSubmissionDto From(BuildSubmission submission) => new()
    {
        Id = submission.Id,
        FeatureId = submission.FeatureId,
        StagingBranch = submission.StagingBranch,
        HeadSha = submission.HeadSha,
        SnapshotVersion = submission.SnapshotVersion,
        Status = submission.Status,
        ConfidenceScore = submission.ConfidenceScore,
        ThresholdAtCheck = submission.ThresholdAtCheck,
        SuspectedFeatureIds = submission.SuspectedFeatureIds,
        Findings = Deserialize<BuildFinding>(submission.FindingsJson),
        Suggestions = Deserialize<BuildSuggestion>(submission.SuggestionsJson),
        CarriedFromSubmissionId = submission.CarriedFromSubmissionId,
        FailureCode = submission.FailureCode,
        CreatedAt = submission.CreatedAt,
        AnalyzedAt = submission.AnalyzedAt,
        ApprovedSha = submission.ApprovedSha,
        ApprovedAt = submission.ApprovedAt
    };

    private static List<T> Deserialize<T>(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<List<T>>(json, AiJson.Options) ?? [];
}
