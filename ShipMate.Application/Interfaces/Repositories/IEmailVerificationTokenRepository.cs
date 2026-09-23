using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token);

    /// <summary>
    /// Looks up a token scoped to a specific user (not globally), since OTP codes are short
    /// and could otherwise collide across different users.
    /// </summary>
    Task<EmailVerificationToken?> GetValidTokenAsync(Guid userId, string tokenHash);

    /// <summary>
    /// Marks every unused token for this user as used, so resending a code can't leave multiple
    /// valid codes alive at once.
    /// </summary>
    Task InvalidateAllUnusedForUserAsync(Guid userId);
}
