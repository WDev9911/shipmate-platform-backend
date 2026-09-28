using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class AiAnalysisRunConfiguration : IEntityTypeConfiguration<AiAnalysisRun>
{
    public void Configure(EntityTypeBuilder<AiAnalysisRun> builder)
    {
        builder.ToTable("ai_analysis_runs");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.ProductDefinitionId).HasColumnName("product_definition_id");
        builder.HasIndex(r => r.ProductDefinitionId);

        builder.Property(r => r.RunByUserId).HasColumnName("run_by_user_id");

        builder.Property(r => r.VisionPromptSnapshot).HasColumnName("vision_prompt_snapshot").HasColumnType("text").IsRequired();
        builder.Property(r => r.Model).HasColumnName("model").HasMaxLength(AiAnalysisRunConstraints.ModelMaxLength).IsRequired();
        builder.Property(r => r.Succeeded).HasColumnName("succeeded");
        builder.Property(r => r.RawResponse).HasColumnName("raw_response").HasColumnType("text");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message").HasColumnType("text");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");

        builder.HasOne(r => r.ProductDefinition)
            .WithMany(p => p.AnalysisRuns)
            .HasForeignKey(r => r.ProductDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.RunBy)
            .WithMany()
            .HasForeignKey(r => r.RunByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
