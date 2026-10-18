using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// A request to change the scope after Lock. The AI proposes a complexity, the requester confirms it, the
/// Manager decides. The row doubles as the audit trail of the proposal and the decision.
/// </summary>
public class FeatureChallenge : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }

    // The id the new feature will have in the snapshot, fixed now so the proposal and the result match.
    public Guid FeatureId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public int? ProposedComplexity { get; set; }
    public int? Complexity { get; set; }

    public ChallengeStatus Status { get; set; } = ChallengeStatus.Open;

    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? RejectionReason { get; set; }

    public ChallengeAction? Action { get; set; }
    public Guid? RemovedFeatureId { get; set; }
    public RemovalDependentsResolution? DependentsResolution { get; set; }
    public Guid? ReplacementFeatureId { get; set; }
    public bool CommittedRemovalConfirmed { get; set; }
    public int? ResultingSnapshotVersion { get; set; }

    // Last Persona Conflict Check of a replacement the Manager considered (Restructure). A warning, never a block.
    public Guid? PersonaCheckFeatureId { get; set; }
    public bool? PersonaConflict { get; set; }
    public string? PersonaConflictReason { get; set; }
}
