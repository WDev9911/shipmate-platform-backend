using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _context;

    public PasswordResetTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PasswordResetToken token) =>
        await _context.PasswordResetTokens.AddAsync(token);

    public async Task<PasswordResetToken?> GetValidTokenAsync(Guid userId, string tokenHash) =>
        await _context.PasswordResetTokens
            .Where(t => t.UserId == userId && t.TokenHash == tokenHash)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<PasswordResetToken?> GetBySessionTicketHashAsync(string sessionTicketHash) =>
        await _context.PasswordResetTokens.FirstOrDefaultAsync(t => t.SessionTicketHash == sessionTicketHash);
}
