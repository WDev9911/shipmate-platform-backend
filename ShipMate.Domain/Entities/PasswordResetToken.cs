using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

public class PasswordResetToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    // Set once the OTP code has been verified. SessionTicketHash is a separate, high-entropy
    // token issued at that point — the FE uses it for the final reset-password call instead
    // of re-submitting the short OTP code.
    public DateTime? VerifiedAt { get; set; }
    public string? SessionTicketHash { get; set; }
}
