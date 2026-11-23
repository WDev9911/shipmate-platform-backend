using Microsoft.EntityFrameworkCore;
using ShipMate.Application.DTOs.Admin;
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

    public async Task<(List<AdminActionLog> Items, int TotalCount)> GetPagedAsync(AdminActionLogQuery filter)
    {
        var query = _context.AdminActionLogs.AsQueryable();

        if (filter.Action is { } action)
        {
            query = query.Where(l => l.Action == action);
        }

        if (filter.AdminUserId is { } adminUserId)
        {
            query = query.Where(l => l.AdminUserId == adminUserId);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(l => l.AdminUser)
            .Include(l => l.TargetUser)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }
}
