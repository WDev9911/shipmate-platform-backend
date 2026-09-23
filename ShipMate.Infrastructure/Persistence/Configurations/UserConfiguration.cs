using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    private static readonly Dictionary<UserRole, string> RoleToDb = new()
    {
        [UserRole.Developer] = "developer",
        [UserRole.Manager] = "manager",
        [UserRole.Administrator] = "admin"
    };

    private static readonly Dictionary<UserStatus, string> StatusToDb = new()
    {
        [UserStatus.Active] = "active",
        [UserStatus.Locked] = "locked",
        [UserStatus.Removed] = "removed"
    };

    private static readonly Dictionary<NotificationPreference, string> NotificationToDb = new()
    {
        [NotificationPreference.Email] = "email",
        [NotificationPreference.InApp] = "in_app",
        [NotificationPreference.Both] = "both"
    };

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
        builder.Property(u => u.HasPassword).HasColumnName("has_password").HasDefaultValue(false);

        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);

        builder.Property(u => u.IsEmailVerified).HasColumnName("is_email_verified").HasDefaultValue(false);
        builder.Property(u => u.EmailVerifiedAt).HasColumnName("email_verified_at");

        builder.Property(u => u.GitHubId).HasColumnName("github_id").HasMaxLength(50);
        builder.HasIndex(u => u.GitHubId).IsUnique();

        builder.Property(u => u.GitHubUsername).HasColumnName("github_username").HasMaxLength(100);

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion(v => RoleToDb[v], v => RoleToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(20)
            .HasDefaultValue(UserRole.Developer);

        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion(v => StatusToDb[v], v => StatusToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(20)
            .HasDefaultValue(UserStatus.Active);

        // No HasDefaultValue here: the enum's CLR default (Email = 0) differs from the DB default (Both),
        // so EF Core would silently overwrite an explicit "Email" value with "Both" if configured.
        // The default is already guaranteed by the entity's property initializer (NotificationPreference.Both).
        builder.Property(u => u.NotificationPreference)
            .HasColumnName("notification_preference")
            .HasConversion(v => NotificationToDb[v], v => NotificationToDb.First(kv => kv.Value == v).Key)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(u => u.GitHubConnection)
            .WithOne(c => c.User)
            .HasForeignKey<GitHubConnection>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(r => r.User)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.EmailVerificationTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.PasswordResetTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
