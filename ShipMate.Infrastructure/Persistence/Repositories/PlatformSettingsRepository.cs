using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class PlatformSettingsRepository : IPlatformSettingsRepository
{
    private readonly AppDbContext _context;

    public PlatformSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformSettings> GetAsync() =>
        await _context.PlatformSettings.FirstOrDefaultAsync(s => s.Id == PlatformSettings.SingleRowId)
            ?? throw new InvalidOperationException(
                "The platform settings row is missing. It is created by the AddPlatformSettings migration; " +
                "make sure the database is migrated rather than inventing a row of defaults.");

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
