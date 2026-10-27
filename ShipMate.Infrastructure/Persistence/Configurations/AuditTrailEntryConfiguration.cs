using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class AuditTrailEntryConfiguration : IEntityTypeConfiguration<AuditTrailEntry>
{
    public void Configure(EntityTypeBuilder<AuditTrailEntry> builder)
    {
        builder.ToTable("audit_trail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(e => e.Sequence).HasColumnName("sequence");
        // Two records can't take the same place in a workspace's chain.
        builder.HasIndex(e => new { e.WorkspaceId, e.Sequence }).IsUnique();

        builder.Property(e => e.AuditType).HasColumnName("audit_type").HasMaxLength(40);
        builder.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
        // text, not jsonb: jsonb rewrites the document, and the hash covers the exact characters written.
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("text");
        builder.Property(e => e.PrevHash).HasColumnName("prev_hash").HasMaxLength(64);
        builder.Property(e => e.RecordHash).HasColumnName("record_hash").HasMaxLength(64);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        // The history outlives the people in it: no foreign key to users, only to the workspace.
        builder.HasOne(e => e.Workspace)
            .WithMany()
            .HasForeignKey(e => e.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
