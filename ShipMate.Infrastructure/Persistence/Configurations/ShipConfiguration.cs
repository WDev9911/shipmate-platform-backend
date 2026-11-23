using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class ShipAttemptConfiguration : IEntityTypeConfiguration<ShipAttempt>
{
    public void Configure(EntityTypeBuilder<ShipAttempt> builder)
    {
        builder.ToTable("ship_attempts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(a => a.SnapshotVersion).HasColumnName("snapshot_version");
        builder.Property(a => a.CreatedByUserId).HasColumnName("created_by_user_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        // The history and the hourly limit both read a workspace's newest attempts.
        builder.HasIndex(a => new { a.WorkspaceId, a.CreatedAt });

        builder.Property(a => a.Url).HasColumnName("url").HasMaxLength(2048);
        builder.Property(a => a.Status).HasColumnName("status").HasSnakeCaseConversion();
        builder.Property(a => a.FailureCode).HasColumnName("failure_code").HasMaxLength(50);

        builder.Property(a => a.PingStatusCode).HasColumnName("ping_status_code");
        builder.Property(a => a.PingError).HasColumnName("ping_error").HasMaxLength(50);
        builder.Property(a => a.SslValid).HasColumnName("ssl_valid");
        builder.Property(a => a.SslError).HasColumnName("ssl_error").HasMaxLength(50);
        builder.Property(a => a.SslExpiresAt).HasColumnName("ssl_expires_at");
        builder.Property(a => a.OwnershipVerified).HasColumnName("ownership_verified");
        builder.Property(a => a.OwnershipError).HasColumnName("ownership_error").HasMaxLength(50);
        builder.Property(a => a.CheckedAt).HasColumnName("checked_at");

        builder.HasOne(a => a.Workspace)
            .WithMany()
            .HasForeignKey(a => a.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ShipCardConfiguration : IEntityTypeConfiguration<ShipCard>
{
    public void Configure(EntityTypeBuilder<ShipCard> builder)
    {
        builder.ToTable("ship_cards");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(c => c.SnapshotVersion).HasColumnName("snapshot_version");
        // One card per version: two requests shipping the same version at once can't both mint one.
        builder.HasIndex(c => new { c.WorkspaceId, c.SnapshotVersion }).IsUnique();

        builder.Property(c => c.ShipAttemptId).HasColumnName("ship_attempt_id");
        builder.Property(c => c.ProductName).HasColumnName("product_name").HasMaxLength(200);
        builder.Property(c => c.Url).HasColumnName("url").HasMaxLength(2048);
        builder.Property(c => c.ShippedAt).HasColumnName("shipped_at");
        builder.Property(c => c.ShippedByUserId).HasColumnName("shipped_by_user_id");
        builder.Property(c => c.TimeToShipSeconds).HasColumnName("time_to_ship_seconds");

        builder.Property(c => c.Score).HasColumnName("score");
        builder.Property(c => c.ScoreFormulaVersion).HasColumnName("score_formula_version").HasMaxLength(20);
        builder.Property(c => c.ScoreBreakdown).HasColumnName("score_breakdown").HasColumnType("jsonb");

        builder.Property(c => c.VerificationChecklistId).HasColumnName("verification_checklist_id");
        builder.Property(c => c.VerificationCompletedAt).HasColumnName("verification_completed_at");

        builder.Property(c => c.AuditAnchorSequence).HasColumnName("audit_anchor_sequence");
        builder.Property(c => c.AuditAnchorHash).HasColumnName("audit_anchor_hash").HasMaxLength(64);

        builder.Property(c => c.IsPublic).HasColumnName("is_public");
        builder.Property(c => c.PublicSlug).HasColumnName("public_slug").HasMaxLength(32);
        builder.HasIndex(c => c.PublicSlug).IsUnique();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        builder.HasOne(c => c.Workspace)
            .WithMany()
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // The attempt that shipped stays with its card. No foreign key to the checklist or the user: the card keeps
        // what it recorded whatever happens to them.
        builder.HasOne<ShipAttempt>()
            .WithOne()
            .HasForeignKey<ShipCard>(c => c.ShipAttemptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
