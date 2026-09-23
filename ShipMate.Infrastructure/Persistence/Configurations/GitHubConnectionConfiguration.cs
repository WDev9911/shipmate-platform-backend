using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class GitHubConnectionConfiguration : IEntityTypeConfiguration<GitHubConnection>
{
    public void Configure(EntityTypeBuilder<GitHubConnection> builder)
    {
        builder.ToTable("github_connections");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.HasIndex(c => c.UserId).IsUnique();

        // AES-256-GCM encrypted string (nonce + ciphertext + tag, base64) — never store the plaintext.
        builder.Property(c => c.GitHubAccessTokenEncrypted)
            .HasColumnName("github_access_token")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(c => c.AccessTokenExpiresAt).HasColumnName("access_token_expires_at");

        builder.Property(c => c.RefreshTokenEncrypted)
            .HasColumnName("github_refresh_token")
            .HasColumnType("text");
        builder.Property(c => c.RefreshTokenExpiresAt).HasColumnName("refresh_token_expires_at");

        builder.Property(c => c.Scope).HasColumnName("scope").HasMaxLength(255);
        builder.Property(c => c.ConnectedAt).HasColumnName("connected_at");
        builder.Property(c => c.RevokedAt).HasColumnName("revoked_at");
    }
}
