using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.UserId).HasColumnName("user_id");

        builder.Property(t => t.TokenHash).HasColumnName("token_hash").HasMaxLength(255).IsRequired();

        // Not globally unique: OTP codes are short (6 digits) and can collide across different users.
        // Lookups are always scoped by (user_id, token_hash) together.
        builder.HasIndex(t => new { t.UserId, t.TokenHash });

        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(t => t.UsedAt).HasColumnName("used_at");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");

        builder.Property(t => t.VerifiedAt).HasColumnName("verified_at");

        // High-entropy (32 random bytes) unlike the OTP token_hash, so a global unique index is safe here.
        builder.Property(t => t.SessionTicketHash).HasColumnName("session_ticket_hash").HasMaxLength(255);
        builder.HasIndex(t => t.SessionTicketHash).IsUnique();
    }
}
