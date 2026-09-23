using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly AppDbContext _context;

    public WorkspaceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id) =>
        await _context.Workspaces.FirstOrDefaultAsync(w => w.Id == id);

    public async Task<List<Workspace>> GetByMemberUserIdAsync(Guid userId) =>
        await _context.Workspaces
            .Where(w => _context.WorkspaceMembers.Any(m =>
                m.WorkspaceId == w.Id && m.UserId == userId && m.Status == WorkspaceMemberStatus.Active))
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

    public async Task<bool> HasManagedWorkspaceWithLinkedGitHubRepoAsync(Guid userId) =>
        await _context.Workspaces.AnyAsync(w =>
            w.GitHubRepoOwner != null &&
            _context.WorkspaceMembers.Any(m =>
                m.WorkspaceId == w.Id && m.UserId == userId &&
                m.Role == WorkspaceMemberRole.Manager && m.Status == WorkspaceMemberStatus.Active));

    public async Task AddAsync(Workspace workspace) =>
        await _context.Workspaces.AddAsync(workspace);

    public void Update(Workspace workspace) =>
        _context.Workspaces.Update(workspace);

    public async Task<bool> SaveChangesAsync() =>
        await _context.SaveChangesAsync() > 0;
}
