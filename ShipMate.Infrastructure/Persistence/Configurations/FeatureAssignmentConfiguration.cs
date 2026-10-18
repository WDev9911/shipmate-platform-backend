using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class FeatureAssignmentConfiguration : IEntityTypeConfiguration<FeatureAssignment>
{
    public void Configure(EntityTypeBuilder<FeatureAssignment> builder)
    {
        builder.ToTable("feature_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(a => a.FeatureId).HasColumnName("feature_id");
        builder.HasIndex(a => a.FeatureId).IsUnique();

        builder.Property(a => a.AssignedUserId).HasColumnName("assigned_user_id");
        builder.Property(a => a.BranchName).HasColumnName("branch_name").HasMaxLength(300);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        builder.HasOne(a => a.Workspace)
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.AssignedUser)
            .WithMany()
            .HasForeignKey(a => a.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
