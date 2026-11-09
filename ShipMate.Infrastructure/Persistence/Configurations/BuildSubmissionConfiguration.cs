using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class BuildSubmissionConfiguration : IEntityTypeConfiguration<BuildSubmission>
{
    public void Configure(EntityTypeBuilder<BuildSubmission> builder)
    {
        builder.ToTable("build_submissions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(s => s.FeatureId).HasColumnName("feature_id");
        builder.HasIndex(s => new { s.WorkspaceId, s.FeatureId });

        builder.Property(s => s.SubmittedByUserId).HasColumnName("submitted_by_user_id");
        builder.Property(s => s.StagingBranch).HasColumnName("staging_branch").HasMaxLength(300);
        builder.Property(s => s.HeadSha).HasColumnName("head_sha").HasMaxLength(40);
        builder.Property(s => s.SnapshotVersion).HasColumnName("snapshot_version");
        builder.Property(s => s.Status).HasColumnName("status").HasSnakeCaseConversion();

        builder.Property(s => s.ConfidenceScore).HasColumnName("confidence_score").HasColumnType("numeric(4,3)");
        builder.Property(s => s.ThresholdAtCheck).HasColumnName("threshold_at_check").HasColumnType("numeric(4,2)");
        builder.Property(s => s.SuspectedFeatureIds).HasColumnName("suspected_feature_ids").HasColumnType("uuid[]");
        builder.Property(s => s.FindingsJson).HasColumnName("findings").HasColumnType("jsonb");
        builder.Property(s => s.SuggestionsJson).HasColumnName("suggestions").HasColumnType("jsonb");
        builder.Property(s => s.CarriedFromSubmissionId).HasColumnName("carried_from_submission_id");
        builder.Property(s => s.AnalyzedAt).HasColumnName("analyzed_at");
        builder.Property(s => s.FailureCode).HasColumnName("failure_code").HasMaxLength(50);
        builder.Property(s => s.AiModel).HasColumnName("ai_model").HasMaxLength(100);
        builder.Property(s => s.AiRawResponse).HasColumnName("ai_raw_response").HasColumnType("text");
        builder.Property(s => s.ApprovedSha).HasColumnName("approved_sha").HasMaxLength(40);
        builder.Property(s => s.ApprovedByUserId).HasColumnName("approved_by_user_id");
        builder.Property(s => s.ApprovedAt).HasColumnName("approved_at");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasOne(s => s.Workspace)
            .WithMany()
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SubmittedByUser)
            .WithMany()
            .HasForeignKey(s => s.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
