using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Interfaces.Services;

public interface IWorkspaceService
{
    Task<WorkspaceDto> CreateAsync(Guid ownerId, CreateWorkspaceRequest request);
    Task<List<WorkspaceDto>> GetMyWorkspacesAsync(Guid ownerId);
    Task<WorkspaceDto> GetByIdAsync(Guid ownerId, Guid workspaceId);
    Task<WorkspaceDto> UpdateAsync(Guid ownerId, Guid workspaceId, UpdateWorkspaceRequest request);
    Task ArchiveAsync(Guid ownerId, Guid workspaceId);

    Task<List<WorkspaceMemberDto>> GetMembersAsync(Guid requesterId, Guid workspaceId);
    Task<WorkspaceMemberDto> AddMemberAsync(Guid requesterId, Guid workspaceId, AddMemberRequest request);
    Task<WorkspaceMemberDto> UpdateMemberRoleAsync(Guid requesterId, Guid workspaceId, Guid memberUserId, UpdateMemberRoleRequest request);
    Task<WorkspaceMemberDto> UpdateMySkillAsync(Guid userId, Guid workspaceId, UpdateMySkillRequest request);
    /// <param name="force">Removes the member even if GitHub can't take their repository access away; the skip is recorded.</param>
    Task RemoveMemberAsync(Guid requesterId, Guid workspaceId, Guid memberUserId, bool force = false);

    Task<List<WorkspaceInvitationDto>> GetMyInvitationsAsync(Guid userId);
    Task<WorkspaceMemberDto> AcceptInvitationAsync(Guid userId, Guid workspaceId, AcceptInvitationRequest request);
    Task DeclineInvitationAsync(Guid userId, Guid workspaceId);

    Task<List<GitHubRepoDto>> GetAvailableGitHubReposAsync(Guid userId, Guid workspaceId);
    Task<WorkspaceDto> LinkGitHubRepoAsync(Guid userId, Guid workspaceId, LinkGitHubRepoRequest request);
    Task UnlinkGitHubRepoAsync(Guid userId, Guid workspaceId);
}
