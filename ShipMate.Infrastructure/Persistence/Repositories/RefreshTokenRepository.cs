using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken token) =>
        await _context.RefreshTokens.AddAsync(token);

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash) =>
        await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

    public async Task<List<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId) =>
        await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
}
