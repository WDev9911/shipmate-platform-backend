using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class AiCallLogConfiguration : IEntityTypeConfiguration<AiCallLog>
{
    private static readonly Dictionary<AiCallPurpose, string> PurposeToDb = new()
    {
        [AiCallPurpose.DefineAnalysis] = "define_analysis",
        [AiCallPurpose.ComplexityScoring] = "complexity_scoring",
        [AiCallPurpose.PersonaConflict] = "persona_conflict",
        [AiCallPurpose.BuildAnalysis] = "build_analysis"
    };

    private static readonly Dictionary<AiCallOutcome, string> OutcomeToDb = new()
    {
        [AiCallOutcome.Succeeded] = "succeeded",
        [AiCallOutcome.RateLimited] = "rate_limited",
        [AiCallOutcome.ProviderUnavailable] = "provider_unavailable",
        [AiCallOutcome.InvalidOutput] = "invalid_output",
        [AiCallOutcome.Cancelled] = "cancelled"
    };

    public void Configure(EntityTypeBuilder<AiCallLog> builder)
    {
        builder.ToTable("ai_call_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.Purpose)
            .HasColumnName("purpose")
            .HasConversion(v => PurposeToDb[v], v => PurposeToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.Model).HasColumnName("model").HasMaxLength(100).IsRequired();

        builder.Property(l => l.Outcome)
            .HasColumnName("outcome")
            .HasConversion(v => OutcomeToDb[v], v => OutcomeToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(l => l.DurationMs).HasColumnName("duration_ms");
        builder.Property(l => l.InputTokens).HasColumnName("input_tokens");
        builder.Property(l => l.OutputTokens).HasColumnName("output_tokens");
        builder.Property(l => l.ThinkingTokens).HasColumnName("thinking_tokens");

        // Every report asks for a range of time.
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(l => l.CreatedAt);
    }
}
