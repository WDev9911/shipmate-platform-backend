using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class VerificationChecklistConfiguration : IEntityTypeConfiguration<VerificationChecklist>
{
    public void Configure(EntityTypeBuilder<VerificationChecklist> builder)
    {
        builder.ToTable("verification_checklists");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(c => c.MvpSnapshotId).HasColumnName("mvp_snapshot_id");
        builder.Property(c => c.SnapshotVersion).HasColumnName("snapshot_version");
        builder.HasIndex(c => new { c.WorkspaceId, c.SnapshotVersion }).IsUnique();

        builder.Property(c => c.AllItemsChecked).HasColumnName("all_items_checked");
        builder.Property(c => c.CompletedAt).HasColumnName("completed_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");

        // A change is saved only if nobody edited the list since it was read, so two people ticking at once
        // can't leave AllItemsChecked saying something neither of them saw.
        builder.Property(c => c.Revision).HasColumnName("revision").IsConcurrencyToken();

        builder.HasOne(c => c.Workspace)
            .WithMany()
            .HasForeignKey(c => c.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // One list for each snapshot; it goes with the snapshot.
        builder.HasOne(c => c.MvpSnapshot)
            .WithOne()
            .HasForeignKey<VerificationChecklist>(c => c.MvpSnapshotId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.ChecklistId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VerificationChecklistItemConfiguration : IEntityTypeConfiguration<VerificationChecklistItem>
{
    public void Configure(EntityTypeBuilder<VerificationChecklistItem> builder)
    {
        builder.ToTable("verification_checklist_items");

        builder.HasKey(i => new { i.ChecklistId, i.ItemKey });

        builder.Property(i => i.ChecklistId).HasColumnName("checklist_id");
        builder.Property(i => i.ItemKey).HasColumnName("item_key").HasMaxLength(40);

        builder.Property(i => i.Applicable).HasColumnName("applicable");
        builder.Property(i => i.NotApplicableReason).HasColumnName("not_applicable_reason").HasMaxLength(500);

        builder.Property(i => i.Checked).HasColumnName("checked");
        builder.Property(i => i.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(i => i.CheckedByUserId).HasColumnName("checked_by_user_id");
        builder.Property(i => i.CheckedAt).HasColumnName("checked_at");
    }
}
