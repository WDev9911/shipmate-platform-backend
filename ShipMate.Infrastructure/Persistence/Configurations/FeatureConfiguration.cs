using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class FeatureConfiguration : IEntityTypeConfiguration<Feature>
{
    public void Configure(EntityTypeBuilder<Feature> builder)
    {
        builder.ToTable("features");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");

        builder.Property(f => f.ProductDefinitionId).HasColumnName("product_definition_id");
        builder.HasIndex(f => f.ProductDefinitionId);

        builder.Property(f => f.Name).HasColumnName("name").HasMaxLength(FeatureConstraints.NameMaxLength).IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(f => f.Scope).HasColumnName("scope").HasColumnType("text");

        builder.Property(f => f.Role)
            .HasColumnName("role")
            .HasSnakeCaseConversion();

        builder.Property(f => f.Origin)
            .HasColumnName("origin")
            .HasSnakeCaseConversion()
            .IsRequired();

        builder.Property(f => f.Sources)
            .HasColumnName("sources")
            .HasSnakeCaseArrayConversion()
            .IsRequired();

        builder.Property(f => f.AiVerdict)
            .HasColumnName("ai_verdict")
            .HasSnakeCaseConversion();

        builder.Property(f => f.AiReason).HasColumnName("ai_reason").HasColumnType("text");

        builder.Property(f => f.Status)
            .HasColumnName("status")
            .HasSnakeCaseConversion()
            .IsRequired();

        builder.Property(f => f.PossibleDuplicate).HasColumnName("possible_duplicate");
        builder.Property(f => f.DuplicateOfId).HasColumnName("duplicate_of_id");
        builder.Property(f => f.DuplicateReason).HasColumnName("duplicate_reason").HasColumnType("text");

        builder.Property(f => f.PersonaConflict).HasColumnName("persona_conflict");
        builder.Property(f => f.PersonaConflictReason).HasColumnName("persona_conflict_reason").HasColumnType("text");

        builder.Property(f => f.DevDecided).HasColumnName("dev_decided");
        builder.Property(f => f.Position).HasColumnName("position");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(f => f.ProductDefinition)
            .WithMany(p => p.Features)
            .HasForeignKey(f => f.ProductDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.DuplicateOf)
            .WithMany()
            .HasForeignKey(f => f.DuplicateOfId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
