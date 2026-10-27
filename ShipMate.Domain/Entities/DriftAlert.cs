using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// Raised when a submission goes beyond the locked scope. The check results are copied in and never change, so the
/// alert keeps saying what was true when it was raised, whatever the scope or threshold become later.
/// </summary>
public class DriftAlert : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid FeatureId { get; set; }
    public Guid SubmissionId { get; set; }
    public BuildSubmission Submission { get; set; } = null!;

    public int SnapshotVersionAtCheck { get; set; }
    public decimal ThresholdAtCheck { get; set; }
    public decimal ConfidenceScore { get; set; }
    public List<Guid> SuspectedFeatureIds { get; set; } = new();

    public DriftAlertStatus Status { get; set; } = DriftAlertStatus.Unresolved;

    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }

    public DateTime? OverriddenAt { get; set; }
    public Guid? OverriddenByUserId { get; set; }
    public string? OverrideReason { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBySubmissionId { get; set; }

    // Open alerts are the ones that still block approval.
    public bool IsOpen => Status is DriftAlertStatus.Unresolved or DriftAlertStatus.Acknowledged;

    public bool Acknowledge(Guid userId, DateTime now)
    {
        if (Status != DriftAlertStatus.Unresolved)
        {
            return false;
        }

        Status = DriftAlertStatus.Acknowledged;
        AcknowledgedAt = now;
        AcknowledgedByUserId = userId;
        return true;
    }

    public bool Override(Guid userId, string reason, DateTime now)
    {
        if (!IsOpen)
        {
            return false;
        }

        Status = DriftAlertStatus.Overridden;
        OverriddenAt = now;
        OverriddenByUserId = userId;
        OverrideReason = reason;
        return true;
    }

    public bool Resolve(Guid bySubmissionId, DateTime now)
    {
        if (!IsOpen)
        {
            return false;
        }

        Status = DriftAlertStatus.Resolved;
        ResolvedAt = now;
        ResolvedBySubmissionId = bySubmissionId;
        return true;
    }
}
