using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class FeatureDependencyConfiguration : IEntityTypeConfiguration<FeatureDependency>
{
    public void Configure(EntityTypeBuilder<FeatureDependency> builder)
    {
        builder.ToTable("feature_dependencies");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.FeatureId).HasColumnName("feature_id");
        builder.Property(d => d.DependsOnFeatureId).HasColumnName("depends_on_feature_id");
        builder.HasIndex(d => new { d.FeatureId, d.DependsOnFeatureId }).IsUnique();

        builder.Property(d => d.Reason).HasColumnName("reason").HasColumnType("text");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at");

        builder.HasOne(d => d.Feature)
            .WithMany(f => f.Dependencies)
            .HasForeignKey(d => d.FeatureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.DependsOnFeature)
            .WithMany()
            .HasForeignKey(d => d.DependsOnFeatureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
