using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class AdminActionLogRepository : IAdminActionLogRepository
{
    private readonly AppDbContext _context;

    public AdminActionLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AdminActionLog log) =>
        await _context.AdminActionLogs.AddAsync(log);

    public async Task<List<AdminActionLog>> GetByTargetUserIdAsync(Guid targetUserId) =>
        await _context.AdminActionLogs
            .Include(l => l.AdminUser)
            .Where(l => l.TargetUserId == targetUserId)
            .ToListAsync();
}
