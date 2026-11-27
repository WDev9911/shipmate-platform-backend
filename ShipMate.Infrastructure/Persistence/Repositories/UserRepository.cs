using Microsoft.EntityFrameworkCore;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string email) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

    public async Task<User?> GetByIdAsync(Guid id) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User?> GetByGitHubIdAsync(string githubId) =>
        await _context.Users.FirstOrDefaultAsync(u => u.GitHubId == githubId);

    public async Task<(List<User> Items, int TotalCount)> GetPagedAsync(AdminUserQuery filter)
    {
        var query = _context.Users.AsQueryable();

        if (filter.Role is { } role)
        {
            query = query.Where(u => u.Role == role);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // % and _ in what the admin typed are letters to look for, not wildcards.
            var pattern = $"%{EscapeLike(filter.Search.Trim())}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Email, pattern, "\\") || EF.Functions.ILike(u.DisplayName, pattern, "\\"));
        }

        var ordered = query.OrderByDescending(u => u.CreatedAt);
        var totalCount = await ordered.CountAsync();
        var items = await ordered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public async Task AddAsync(User user) =>
        await _context.Users.AddAsync(user);

    public void Update(User user) =>
        _context.Users.Update(user);

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
