namespace ShipMate.Domain.Entities;

/// <summary>
/// Kept separate from User: the GitHub access token is encrypted at rest and never returned to the FE.
/// </summary>
public class GitHubConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string GitHubAccessTokenEncrypted { get; set; } = string.Empty;
    public DateTime? AccessTokenExpiresAt { get; set; }

    // GitHub's own refresh token (separate from ShipMate's refresh token), used to renew the
    // access token once it expires. Also encrypted at rest.
    public string? RefreshTokenEncrypted { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }

    public string Scope { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}
