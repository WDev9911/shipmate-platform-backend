using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class FeatureChallengeConfiguration : IEntityTypeConfiguration<FeatureChallenge>
{
    public void Configure(EntityTypeBuilder<FeatureChallenge> builder)
    {
        builder.ToTable("feature_challenges");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.WorkspaceId).HasColumnName("workspace_id");
        builder.HasIndex(c => c.WorkspaceId);

        builder.Property(c => c.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(c => c.FeatureId).HasColumnName("feature_id");

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(FeatureConstraints.NameMaxLength);
        builder.Property(c => c.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(c => c.Reason).HasColumnName("reason").HasColumnType("text");

        builder.Property(c => c.ProposedComplexity).HasColumnName("proposed_complexity");
        builder.Property(c => c.Complexity).HasColumnName("complexity");

        builder.Property(c => c.Status).HasColumnName("status").HasSnakeCaseConversion();

        builder.Property(c => c.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(c => c.DecidedAt).HasColumnName("decided_at");
        builder.Property(c => c.RejectionReason).HasColumnName("rejection_reason").HasColumnType("text");

        builder.Property(c => c.Action).HasColumnName("action").HasSnakeCaseConversion();
        builder.Property(c => c.RemovedFeatureId).HasColumnName("removed_feature_id");
        builder.Property(c => c.DependentsResolution).HasColumnName("dependents_resolution").HasSnakeCaseConversion();
        builder.Property(c => c.ReplacementFeatureId).HasColumnName("replacement_feature_id");
        builder.Property(c => c.CommittedRemovalConfirmed).HasColumnName("committed_removal_confirmed");
        builder.Property(c => c.ResultingSnapshotVersion).HasColumnName("resulting_snapshot_version");
        builder.Property(c => c.PersonaCheckFeatureId).HasColumnName("persona_check_feature_id");
        builder.Property(c => c.PersonaConflict).HasColumnName("persona_conflict");
        builder.Property(c => c.PersonaConflictReason).HasColumnName("persona_conflict_reason").HasColumnType("text");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        builder.HasOne(c => c.Workspace)
            .WithMany()
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
