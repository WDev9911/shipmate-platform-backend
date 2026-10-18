using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class SubmitChallengeRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class ConfirmChallengeComplexityRequest
{
    public int Complexity { get; set; }
}

public class ApproveChallengeRequest
{
    public ChallengeAction Action { get; set; }

    // Remove & Replace only.
    public Guid? RemoveFeatureId { get; set; }
    public RemovalDependentsResolution? DependentsResolution { get; set; }
    public Guid? ReplacementFeatureId { get; set; }

    // Required when the feature being removed came from the committed list.
    public bool ConfirmCommittedRemoval { get; set; }
}

public class RejectChallengeRequest
{
    public string? Reason { get; set; }
}

public class FeatureChallengeDto
{
    public Guid Id { get; set; }
    public Guid FeatureId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? ProposedComplexity { get; set; }
    public int? Complexity { get; set; }
    public ChallengeStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    // Only while the challenge is open and its complexity is confirmed.
    public ChallengeRecommendationDto? Recommendation { get; set; }

    // Only once decided.
    public ChallengeAction? Action { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int? ResultingSnapshotVersion { get; set; }
}

public class ChallengeRecommendationDto
{
    // Null when nothing fits: even removing any single feature would not make room.
    public ChallengeAction? Action { get; set; }
    public RemovalCandidateDto? FeatureToRemove { get; set; }

    // Features hit by removing the candidate, directly or through other features, in one list.
    public List<FeatureReferenceDto> AffectedDependents { get; set; } = new();
}

public record RemovalCandidateDto(Guid Id, string Name, int Complexity, bool IsCommitted);
