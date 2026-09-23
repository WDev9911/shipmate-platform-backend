using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class EmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    private readonly AppDbContext _context;

    public EmailVerificationTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EmailVerificationToken token) =>
        await _context.EmailVerificationTokens.AddAsync(token);

    public async Task<EmailVerificationToken?> GetValidTokenAsync(Guid userId, string tokenHash) =>
        await _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.TokenHash == tokenHash)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task InvalidateAllUnusedForUserAsync(Guid userId)
    {
        var tokens = await _context.EmailVerificationTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }
    }
}
