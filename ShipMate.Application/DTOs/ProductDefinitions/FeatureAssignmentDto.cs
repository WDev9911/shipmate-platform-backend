using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>One locked feature with who may take it and who already did.</summary>
public class FeatureAssignmentOverviewDto
{
    public Guid FeatureId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SnapshotFeatureStatus Status { get; set; }
    public int Complexity { get; set; }
    public FeatureAssigneeDto? Assignee { get; set; }
    public List<QualifiedMemberDto> QualifiedMembers { get; set; } = new();
    public FeatureBuildProgressDto Build { get; set; } = new();
}

/// <summary>Where the feature stands in BUILD. Details of a submission: GET .../build/submissions/{id}.</summary>
public class FeatureBuildProgressDto
{
    public FeatureBuildState State { get; set; }
    public Guid? LatestSubmissionId { get; set; }
    public DateTime? LatestSubmittedAt { get; set; }

    // Set when State is Failed, e.g. DIFF_TOO_LARGE.
    public string? FailureCode { get; set; }

    // The unresolved or acknowledged alert holding the feature, if any.
    public Guid? OpenAlertId { get; set; }

    // The most recent approval, which may be older than the newest submission (approved, then more code submitted).
    public FeatureApprovalDto? LastApproval { get; set; }
}

public record FeatureApprovalDto(
    Guid SubmissionId, DateTime ApprovedAt, Guid? ApprovedByUserId, string? ApprovedByName, string? ApprovedSha);

public record FeatureAssigneeDto(Guid UserId, string DisplayName, string BranchName, bool BranchCreated);

public record QualifiedMemberDto(Guid UserId, string DisplayName, int SkillLevel);

public class FeatureAssignmentDto
{
    public Guid FeatureId { get; set; }
    public Guid AssignedUserId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    // False when the workspace has no linked repository or GitHub refused; the claim still stands and can be retried.
    public bool BranchCreated { get; set; }
}
