using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class AdminActionLogConfiguration : IEntityTypeConfiguration<AdminActionLog>
{
    private static readonly Dictionary<AdminAction, string> ActionToDb = new()
    {
        [AdminAction.StatusChanged] = "status_changed",
        [AdminAction.RoleChanged] = "role_changed"
    };

    public void Configure(EntityTypeBuilder<AdminActionLog> builder)
    {
        builder.ToTable("admin_action_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.AdminUserId).HasColumnName("admin_user_id");
        builder.Property(l => l.TargetUserId).HasColumnName("target_user_id");
        builder.HasIndex(l => l.TargetUserId);

        builder.Property(l => l.Action)
            .HasColumnName("action")
            .HasConversion(v => ActionToDb[v], v => ActionToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.OldValue).HasColumnName("old_value").HasMaxLength(50);
        builder.Property(l => l.NewValue).HasColumnName("new_value").HasMaxLength(50);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        // Preserve audit history even if a referenced account is ever removed — never cascade-delete logs.
        builder.HasOne(l => l.AdminUser)
            .WithMany()
            .HasForeignKey(l => l.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.TargetUser)
            .WithMany()
            .HasForeignKey(l => l.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
