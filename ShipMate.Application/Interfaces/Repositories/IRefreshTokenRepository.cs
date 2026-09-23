using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);
    Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId);

    /// <summary>Every session (active, expired, revoked) for a user, newest first — used for login history.</summary>
    Task<List<RefreshToken>> GetAllByUserIdAsync(Guid userId);
}
