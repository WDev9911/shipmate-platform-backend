using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.UserId).HasColumnName("user_id");
        builder.HasIndex(r => r.UserId);

        // SHA-256 hex = 64 chars; leaving headroom in case the hashing algorithm changes later.
        builder.Property(r => r.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();
        builder.HasIndex(r => r.TokenHash).IsUnique();

        builder.Property(r => r.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(r => r.RevokedAt).HasColumnName("revoked_at");
        builder.Property(r => r.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
        builder.Property(r => r.DeviceInfo).HasColumnName("device_info").HasMaxLength(255);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
    }
}
