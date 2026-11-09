using NSubstitute;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class WorkspaceSetupServiceTests
{
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IGitHubConnectionRepository _connections = Substitute.For<IGitHubConnectionRepository>();
    private readonly WorkspaceSetupService _service;
    private readonly Workspace _workspace = FullySetUp();
    private readonly Guid _manager = Guid.NewGuid();
    private readonly List<WorkspaceMember> _roster = [];

    public WorkspaceSetupServiceTests()
    {
        _service = new WorkspaceSetupService(_guard, _members, _connections);

        _guard.GetWorkspaceAsManagerAsync(_manager, _workspace.Id).Returns(_workspace);
        _members.GetByWorkspaceIdAsync(_workspace.Id).Returns(_ => _roster);

        GivenMember(skill: 4, gitHub: true);
    }

    private static Workspace FullySetUp() => new()
    {
        GitHubRepoOwner = "shipmate-test-org",
        GitHubRepoName = "demo-app",
        GitHubInstallationId = 42,
        GitHubRepoIsOrganization = true,
        GitHubMergeGateEnabled = true
    };

    private Guid GivenMember(int? skill, bool gitHub, WorkspaceMemberStatus status = WorkspaceMemberStatus.Active)
    {
        var userId = Guid.NewGuid();
        _roster.Add(new WorkspaceMember { WorkspaceId = _workspace.Id, UserId = userId, SkillLevel = skill, Status = status });
        _connections.GetByUserIdAsync(userId).Returns(gitHub ? new GitHubConnection { UserId = userId } : null);
        return userId;
    }

    private Task<ShipMate.Application.DTOs.Workspaces.WorkspaceSetupDto> Check() => _service.GetAsync(_manager, _workspace.Id);

    [Fact]
    public async Task FullySetUpWorkspace_IsReady()
    {
        var result = await Check();

        Assert.True(result.Ready);
        Assert.True(result.RepoLinked);
        Assert.True(result.AppInstalled);
        Assert.True(result.MergeGateEnabled);
        Assert.True(result.RepoIsOrganization);
        Assert.False(result.GovernanceSuspended);
        Assert.Empty(result.MembersWithoutSkill);
        Assert.Empty(result.MembersWithoutGitHub);
    }

    [Fact]
    public async Task NoRepoLinked_IsNotReady()
    {
        _workspace.GitHubRepoOwner = null;
        _workspace.GitHubRepoName = null;

        var result = await Check();

        Assert.False(result.RepoLinked);
        Assert.False(result.Ready);
    }

    [Fact]
    public async Task RepoLinkedBeforeTheAppExisted_IsLinkedButHasNoApp()
    {
        _workspace.GitHubInstallationId = null;

        var result = await Check();

        Assert.True(result.RepoLinked);
        Assert.False(result.AppInstalled);
        Assert.False(result.Ready);
    }

    [Fact]
    public async Task MergeGateOff_IsNotReady()
    {
        _workspace.GitHubMergeGateEnabled = false;

        var result = await Check();

        Assert.False(result.MergeGateEnabled);
        Assert.False(result.Ready);
    }

    [Fact]
    public async Task PersonalRepo_StaysReadyAndOnlyFlagsItself()
    {
        _workspace.GitHubRepoIsOrganization = false;

        var result = await Check();

        Assert.False(result.RepoIsOrganization);
        Assert.True(result.Ready);
    }

    [Fact]
    public async Task GovernanceSuspended_IsNotReady()
    {
        _workspace.GovernanceSuspendedAt = DateTime.UtcNow;

        var result = await Check();

        Assert.True(result.GovernanceSuspended);
        Assert.False(result.Ready);
    }

    [Fact]
    public async Task MemberWithoutSkill_IsListedAndBlocks()
    {
        var missing = GivenMember(skill: null, gitHub: true);

        var result = await Check();

        Assert.Equal([missing], result.MembersWithoutSkill);
        Assert.False(result.Ready);
    }

    [Fact]
    public async Task MemberWithoutGitHub_IsListedAndBlocks()
    {
        var missing = GivenMember(skill: 3, gitHub: false);

        var result = await Check();

        Assert.Equal([missing], result.MembersWithoutGitHub);
        Assert.False(result.Ready);
    }

    [Fact]
    public async Task RevokedGitHubConnection_CountsAsNotConnected()
    {
        var userId = GivenMember(skill: 3, gitHub: false);
        _connections.GetByUserIdAsync(userId).Returns(new GitHubConnection { UserId = userId, RevokedAt = DateTime.UtcNow });

        var result = await Check();

        Assert.Equal([userId], result.MembersWithoutGitHub);
    }

    [Fact]
    public async Task InvitedOrRemovedMembers_AreNotCounted()
    {
        GivenMember(skill: null, gitHub: false, WorkspaceMemberStatus.Invited);
        GivenMember(skill: null, gitHub: false, WorkspaceMemberStatus.Removed);

        var result = await Check();

        Assert.Empty(result.MembersWithoutSkill);
        Assert.Empty(result.MembersWithoutGitHub);
        Assert.True(result.Ready);
    }

    [Fact]
    public async Task ANonManager_IsRefused()
    {
        var developer = Guid.NewGuid();
        _guard.GetWorkspaceAsManagerAsync(developer, _workspace.Id)
            .Returns<Workspace>(_ => throw new WorkspacePermissionDeniedException());

        await Assert.ThrowsAsync<WorkspacePermissionDeniedException>(() => _service.GetAsync(developer, _workspace.Id));
    }
}
