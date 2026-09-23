using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IWorkspaceMemberRepository
{
    Task<WorkspaceMember?> GetByWorkspaceAndUserIdAsync(Guid workspaceId, Guid userId);
    Task<List<WorkspaceMember>> GetByWorkspaceIdAsync(Guid workspaceId);

    /// <summary>Pending (Invited) memberships for a user, across all workspaces, with Workspace loaded.</summary>
    Task<List<WorkspaceMember>> GetPendingInvitationsByUserIdAsync(Guid userId);
    Task AddAsync(WorkspaceMember member);
    void Update(WorkspaceMember member);
}
