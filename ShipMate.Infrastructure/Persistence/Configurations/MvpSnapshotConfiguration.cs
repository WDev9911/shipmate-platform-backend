using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class MvpSnapshotConfiguration : IEntityTypeConfiguration<MvpSnapshot>
{
    public void Configure(EntityTypeBuilder<MvpSnapshot> builder)
    {
        builder.ToTable("mvp_snapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(s => s.Version).HasColumnName("version");
        builder.HasIndex(s => new { s.WorkspaceId, s.Version }).IsUnique();

        builder.Property(s => s.Hash).HasColumnName("hash").HasMaxLength(64);
        builder.Property(s => s.DevCountAtLock).HasColumnName("dev_count_at_lock");
        builder.Property(s => s.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");

        builder.HasOne(s => s.Workspace)
            .WithMany()
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SnapshotFeatureConfiguration : IEntityTypeConfiguration<SnapshotFeature>
{
    public void Configure(EntityTypeBuilder<SnapshotFeature> builder)
    {
        builder.ToTable("snapshot_features");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");

        builder.Property(f => f.SnapshotId).HasColumnName("snapshot_id");
        builder.Property(f => f.FeatureId).HasColumnName("feature_id");
        builder.HasIndex(f => new { f.SnapshotId, f.FeatureId }).IsUnique();

        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(FeatureConstraints.NameMaxLength);
        builder.Property(f => f.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(f => f.Scope).HasColumnName("scope").HasColumnType("text");
        builder.Property(f => f.Role).HasColumnName("role").HasSnakeCaseConversion();
        builder.Property(f => f.Origin).HasColumnName("origin").HasSnakeCaseConversion();
        builder.Property(f => f.Complexity).HasColumnName("complexity");
        builder.Property(f => f.Status).HasColumnName("status").HasSnakeCaseConversion();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at");

        builder.HasOne(f => f.Snapshot)
            .WithMany(s => s.Features)
            .HasForeignKey(f => f.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SnapshotDependencyConfiguration : IEntityTypeConfiguration<SnapshotDependency>
{
    public void Configure(EntityTypeBuilder<SnapshotDependency> builder)
    {
        builder.ToTable("snapshot_dependencies");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.SnapshotId).HasColumnName("snapshot_id");
        builder.Property(d => d.FeatureId).HasColumnName("feature_id");
        builder.Property(d => d.DependsOnFeatureId).HasColumnName("depends_on_feature_id");
        builder.HasIndex(d => new { d.SnapshotId, d.FeatureId, d.DependsOnFeatureId }).IsUnique();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");

        builder.HasOne(d => d.Snapshot)
            .WithMany(s => s.Dependencies)
            .HasForeignKey(d => d.SnapshotId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
