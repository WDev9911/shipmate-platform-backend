using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public bool HasPassword { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }

    public bool IsEmailVerified { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }

    public string? GitHubId { get; set; }
    public string? GitHubUsername { get; set; }

    public UserRole Role { get; set; } = UserRole.Developer;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public NotificationPreference NotificationPreference { get; set; } = NotificationPreference.Both;

    public DateTime? UpdatedAt { get; set; }

    public GitHubConnection? GitHubConnection { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = new List<EmailVerificationToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
