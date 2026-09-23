using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token);

    /// <summary>
    /// Looks up a token scoped to a specific user (not globally), since OTP codes are short
    /// and could otherwise collide across different users.
    /// </summary>
    Task<PasswordResetToken?> GetValidTokenAsync(Guid userId, string tokenHash);

    /// <summary>
    /// Looks up by the high-entropy session ticket issued after OTP verification — safe to look up
    /// globally since collisions are effectively impossible.
    /// </summary>
    Task<PasswordResetToken?> GetBySessionTicketHashAsync(string sessionTicketHash);
}
