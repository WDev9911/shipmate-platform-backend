using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class DriftAlertConfiguration : IEntityTypeConfiguration<DriftAlert>
{
    public void Configure(EntityTypeBuilder<DriftAlert> builder)
    {
        builder.ToTable("drift_alerts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.Property(a => a.SubmissionId).HasColumnName("submission_id");
        builder.HasIndex(a => new { a.WorkspaceId, a.FeatureId });

        builder.Property(a => a.SnapshotVersionAtCheck).HasColumnName("snapshot_version_at_check");
        builder.Property(a => a.ThresholdAtCheck).HasColumnName("threshold_at_check").HasColumnType("numeric(4,2)");
        builder.Property(a => a.ConfidenceScore).HasColumnName("confidence_score").HasColumnType("numeric(4,3)");
        builder.Property(a => a.SuspectedFeatureIds).HasColumnName("suspected_feature_ids").HasColumnType("uuid[]");

        builder.Property(a => a.Status).HasColumnName("status").HasSnakeCaseConversion();

        builder.Property(a => a.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(a => a.AcknowledgedByUserId).HasColumnName("acknowledged_by_user_id");
        builder.Property(a => a.OverriddenAt).HasColumnName("overridden_at");
        builder.Property(a => a.OverriddenByUserId).HasColumnName("overridden_by_user_id");
        builder.Property(a => a.OverrideReason).HasColumnName("override_reason").HasMaxLength(1000);
        builder.Property(a => a.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(a => a.ResolvedBySubmissionId).HasColumnName("resolved_by_submission_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        builder.HasOne(a => a.Workspace)
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Submission)
            .WithMany()
            .HasForeignKey(a => a.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
