using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Infrastructure.Persistence.Conversions;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class ProductDefinitionConfiguration : IEntityTypeConfiguration<ProductDefinition>
{
    public void Configure(EntityTypeBuilder<ProductDefinition> builder)
    {
        builder.ToTable("product_definitions");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.WorkspaceId).HasColumnName("workspace_id");
        builder.HasIndex(p => p.WorkspaceId).IsUnique();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasSnakeCaseConversion()
            .HasDefaultValue(ProductDefinitionStatus.Draft);

        builder.OwnsOne(p => p.LockedPersona, persona =>
        {
            persona.ToJson("locked_persona");
            persona.Property(lp => lp.PrimaryPersona).HasJsonPropertyName("primary_persona");
            persona.Property(lp => lp.Reason).HasJsonPropertyName("reason");
            persona.OwnsMany(lp => lp.SupportingRoles, role =>
            {
                role.HasJsonPropertyName("supporting_roles");
                role.Property(r => r.Name).HasJsonPropertyName("name");
                role.Property(r => r.Reason).HasJsonPropertyName("reason");
            });
        });

        builder.Property(p => p.Problem).HasColumnName("problem").HasColumnType("text");
        builder.Property(p => p.Solution).HasColumnName("solution").HasColumnType("text");

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.Workspace)
            .WithOne()
            .HasForeignKey<ProductDefinition>(p => p.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
