using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Build;

public class DriftAlertDto
{
    public Guid Id { get; set; }
    public Guid FeatureId { get; set; }
    public Guid SubmissionId { get; set; }

    // What the check found when the alert was raised.
    public int SnapshotVersionAtCheck { get; set; }
    public decimal ThresholdAtCheck { get; set; }
    public decimal ConfidenceScore { get; set; }
    public List<Guid> SuspectedFeatureIds { get; set; } = new();

    public DriftAlertStatus Status { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByUserId { get; set; }
    public DateTime? OverriddenAt { get; set; }
    public Guid? OverriddenByUserId { get; set; }
    public string? OverrideReason { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBySubmissionId { get; set; }

    public DateTime CreatedAt { get; set; }
}
