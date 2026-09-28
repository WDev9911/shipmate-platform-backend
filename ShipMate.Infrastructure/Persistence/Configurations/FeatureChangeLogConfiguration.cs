using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class FeatureChangeLogConfiguration : IEntityTypeConfiguration<FeatureChangeLog>
{
    public void Configure(EntityTypeBuilder<FeatureChangeLog> builder)
    {
        builder.ToTable("feature_change_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.FeatureId).HasColumnName("feature_id");
        builder.HasIndex(l => l.FeatureId);

        builder.Property(l => l.Action)
            .HasColumnName("action")
            .HasSnakeCaseConversion()
            .IsRequired();

        builder.Property(l => l.OldContent).HasColumnName("old_content").HasColumnType("jsonb");
        builder.Property(l => l.NewContent).HasColumnName("new_content").HasColumnType("jsonb");
        builder.Property(l => l.Reason).HasColumnName("reason").HasColumnType("text");
        builder.Property(l => l.CustomerNotifiedConfirmed).HasColumnName("customer_notified_confirmed");

        builder.Property(l => l.PerformedByUserId).HasColumnName("performed_by_user_id");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        // Audit history must outlive everything it references — never cascade-delete logs.
        builder.HasOne(l => l.Feature)
            .WithMany(f => f.ChangeLogs)
            .HasForeignKey(l => l.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.PerformedBy)
            .WithMany()
            .HasForeignKey(l => l.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
