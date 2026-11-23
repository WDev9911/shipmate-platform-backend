using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class PlatformSettingsConfiguration : IEntityTypeConfiguration<PlatformSettings>
{
    public void Configure(EntityTypeBuilder<PlatformSettings> builder)
    {
        // A second row would leave two answers to "what is the budget", so the table can only ever hold row 1.
        builder.ToTable("platform_settings", table =>
            table.HasCheckConstraint("ck_platform_settings_single_row", "id = 1"));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.ComplexityBudgetPerDeveloper).HasColumnName("complexity_budget_per_developer");
        builder.Property(s => s.DefaultDriftThreshold).HasColumnName("default_drift_threshold").HasColumnType("numeric(4,2)");
        builder.Property(s => s.MaintenanceCommitPatterns).HasColumnName("maintenance_commit_patterns").HasColumnType("text[]");

        // A save goes through only if the row is still at the revision it was read at.
        builder.Property(s => s.Revision).HasColumnName("revision").IsConcurrencyToken();

        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedByUserId).HasColumnName("updated_by_user_id");

        // Constants only: a value that changes (such as the current time) would make every new migration touch this row.
        builder.HasData(new PlatformSettings
        {
            Id = PlatformSettings.SingleRowId,
            ComplexityBudgetPerDeveloper = PlatformSettings.InitialComplexityBudgetPerDeveloper,
            DefaultDriftThreshold = PlatformSettings.InitialDriftThreshold,
            MaintenanceCommitPatterns = MaintenanceChangePolicy.DefaultPatterns.ToList(),
            Revision = 0
        });
    }
}
