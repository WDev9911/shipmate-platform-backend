using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    private static readonly Dictionary<WorkspaceMemberRole, string> RoleToDb = new()
    {
        [WorkspaceMemberRole.Developer] = "developer",
        [WorkspaceMemberRole.Manager] = "manager"
    };

    private static readonly Dictionary<WorkspaceMemberStatus, string> StatusToDb = new()
    {
        [WorkspaceMemberStatus.Active] = "active",
        [WorkspaceMemberStatus.Invited] = "invited",
        [WorkspaceMemberStatus.Removed] = "removed"
    };

    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("workspace_members");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");

        // A user can only appear once per workspace.
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion(v => RoleToDb[v], v => RoleToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(20)
            .HasDefaultValue(WorkspaceMemberRole.Developer);

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion(v => StatusToDb[v], v => StatusToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(20)
            .HasDefaultValue(WorkspaceMemberStatus.Active);

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(m => m.Workspace)
            .WithMany()
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
