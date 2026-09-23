using Microsoft.EntityFrameworkCore;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class WorkspaceMemberRepository : IWorkspaceMemberRepository
{
    private readonly AppDbContext _context;

    public WorkspaceMemberRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkspaceMember?> GetByWorkspaceAndUserIdAsync(Guid workspaceId, Guid userId) =>
        await _context.WorkspaceMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);

    public async Task<List<WorkspaceMember>> GetByWorkspaceIdAsync(Guid workspaceId) =>
        await _context.WorkspaceMembers
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == workspaceId && m.Status != WorkspaceMemberStatus.Removed)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<WorkspaceMember>> GetPendingInvitationsByUserIdAsync(Guid userId) =>
        await _context.WorkspaceMembers
            .Include(m => m.Workspace)
            .Where(m => m.UserId == userId && m.Status == WorkspaceMemberStatus.Invited)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(WorkspaceMember member) =>
        await _context.WorkspaceMembers.AddAsync(member);

    public void Update(WorkspaceMember member) =>
        _context.WorkspaceMembers.Update(member);
}
