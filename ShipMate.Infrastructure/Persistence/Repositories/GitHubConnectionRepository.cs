using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class GitHubConnectionRepository : IGitHubConnectionRepository
{
    private readonly AppDbContext _context;

    public GitHubConnectionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GitHubConnection?> GetByUserIdAsync(Guid userId) =>
        await _context.GitHubConnections.FirstOrDefaultAsync(c => c.UserId == userId);

    public async Task AddAsync(GitHubConnection connection) =>
        await _context.GitHubConnections.AddAsync(connection);

    public void Update(GitHubConnection connection) =>
        _context.GitHubConnections.Update(connection);
}
