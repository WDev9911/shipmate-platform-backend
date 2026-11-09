using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class WorkspaceSetupService : IWorkspaceSetupService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IWorkspaceMemberRepository _memberRepository;
    private readonly IGitHubConnectionRepository _gitHubConnectionRepository;

    public WorkspaceSetupService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IWorkspaceMemberRepository memberRepository,
        IGitHubConnectionRepository gitHubConnectionRepository)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _memberRepository = memberRepository;
        _gitHubConnectionRepository = gitHubConnectionRepository;
    }

    public async Task<WorkspaceSetupDto> GetAsync(Guid userId, Guid workspaceId)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var members = (await _memberRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(m => m.Status == WorkspaceMemberStatus.Active)
            .ToList();

        var withoutGitHub = new List<Guid>();
        foreach (var member in members)
        {
            var connection = await _gitHubConnectionRepository.GetByUserIdAsync(member.UserId);
            if (connection is null || connection.RevokedAt is not null)
            {
                withoutGitHub.Add(member.UserId);
            }
        }

        var result = new WorkspaceSetupDto
        {
            RepoLinked = workspace.GitHubRepoOwner is not null && workspace.GitHubRepoName is not null,
            AppInstalled = workspace.GitHubInstallationId is not null,
            MergeGateEnabled = workspace.GitHubMergeGateEnabled,
            RepoIsOrganization = workspace.GitHubRepoIsOrganization,
            GovernanceSuspended = workspace.GovernanceSuspendedAt is not null,
            MembersWithoutSkill = members.Where(m => m.SkillLevel is null).Select(m => m.UserId).ToList(),
            MembersWithoutGitHub = withoutGitHub
        };

        result.Ready = result.RepoLinked
            && result.AppInstalled
            && result.MergeGateEnabled
            && !result.GovernanceSuspended
            && result.MembersWithoutSkill.Count == 0
            && result.MembersWithoutGitHub.Count == 0;

        return result;
    }
}
