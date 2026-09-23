using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    private static readonly Dictionary<WorkspaceStatus, string> StatusToDb = new()
    {
        [WorkspaceStatus.Active] = "active",
        [WorkspaceStatus.Archived] = "archived"
    };

    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");

        builder.Property(w => w.OwnerId).HasColumnName("owner_id");
        builder.HasIndex(w => w.OwnerId);

        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(w => w.VisionPrompt).HasColumnName("vision_prompt").HasColumnType("text").IsRequired();
        builder.Property(w => w.LaunchDeadline).HasColumnName("launch_deadline");

        builder.Property(w => w.Status)
            .HasColumnName("status")
            .HasConversion(v => StatusToDb[v], v => StatusToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(20)
            .HasDefaultValue(WorkspaceStatus.Active);

        builder.Property(w => w.GitHubRepoOwner).HasColumnName("github_repo_owner").HasMaxLength(100);
        builder.Property(w => w.GitHubRepoName).HasColumnName("github_repo_name").HasMaxLength(100);
        builder.Property(w => w.WebhookSecretEncrypted).HasColumnName("webhook_secret").HasColumnType("text");
        builder.Property(w => w.DriftConfidenceThreshold)
            .HasColumnName("drift_confidence_threshold")
            .HasColumnType("numeric(4,2)")
            .HasDefaultValue(0.75m);

        builder.Property(w => w.FirstLockedAt).HasColumnName("first_locked_at");

        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(w => w.Owner)
            .WithMany()
            .HasForeignKey(w => w.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
