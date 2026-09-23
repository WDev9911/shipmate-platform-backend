using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

/// <summary>
/// Only the SHA-256 hash of the token is stored, never the plaintext.
/// ReplacedByTokenId traces the rotation chain and helps detect reuse (a stolen token).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? DeviceInfo { get; set; }
}
