using AutoMapper;
using NSubstitute;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// A member who is no longer in the workspace must not keep the repository, and one who can't be taken off it stays a member.
public class RepoRevocationTests
{
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
    private readonly IAuditTrail _audit = Substitute.For<IAuditTrail>();
    private readonly WorkspaceService _service;
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Workspace _workspace = new()
    {
        GitHubRepoOwner = "shipmate-test-org",
        GitHubRepoName = "demo-app",
        GitHubInstallationId = 42
    };

    public RepoRevocationTests()
    {
        _service = new WorkspaceService(
            _workspaces, _members, _guard,
            Substitute.For<IProductDefinitionRepository>(), Substitute.For<IUserRepository>(),
            Substitute.For<IGitHubConnectionRepository>(), Substitute.For<IGitHubOAuthService>(), _app,
            Substitute.For<IEncryptionService>(), Substitute.For<IEmailSender>(),
            Substitute.For<IWorkspaceNotifier>(), _audit, Substitute.For<IFeatureAssignmentRepository>(), Substitute.For<IMapper>());

        _guard.GetWorkspaceAsManagerAsync(_managerId, _workspace.Id).Returns(_workspace);
        _members.GetByWorkspaceIdAsync(_workspace.Id).Returns(_ => []);
    }

    private WorkspaceMember Member(string? gitHubName, WorkspaceMemberRole role = WorkspaceMemberRole.Developer)
    {
        var user = new User { GitHubUsername = gitHubName };
        var member = new WorkspaceMember
        {
            WorkspaceId = _workspace.Id, UserId = user.Id, User = user, Role = role, Status = WorkspaceMemberStatus.Active
        };
        _members.GetByWorkspaceAndUserIdAsync(_workspace.Id, user.Id).Returns(member);
        return member;
    }

    private Task Remove(WorkspaceMember member, bool force = false) =>
        _service.RemoveMemberAsync(_managerId, _workspace.Id, member.UserId, force);

    private void GitHubFails() =>
        _app.RemoveCollaboratorAsync(42, "shipmate-test-org", "demo-app", Arg.Any<string>())
            .Returns(Task.FromException(new GitHubRequestFailedException()));

    [Fact]
    public async Task Removing_TakesTheRepoAccessAwayFirst_ThenMarksTheMemberRemoved()
    {
        var member = Member("hbob8686");
        var statusWhenGitHubWasCalled = WorkspaceMemberStatus.Removed;
        _app.When(a => a.RemoveCollaboratorAsync(42, "shipmate-test-org", "demo-app", "hbob8686"))
            .Do(_ => statusWhenGitHubWasCalled = member.Status);

        await Remove(member);

        Assert.Equal(WorkspaceMemberStatus.Active, statusWhenGitHubWasCalled);
        Assert.Equal(WorkspaceMemberStatus.Removed, member.Status);
    }

    [Fact]
    public async Task Removing_WhenGitHubFails_KeepsTheMemberAndSaysSo()
    {
        var member = Member("hbob8686");
        GitHubFails();

        await Assert.ThrowsAsync<GitHubRevokeFailedException>(() => Remove(member));

        Assert.Equal(WorkspaceMemberStatus.Active, member.Status);
        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ForcedRemoval_WhenGitHubFails_GoesThroughAndLeavesARecord()
    {
        var member = Member("hbob8686");
        GitHubFails();

        await Remove(member, force: true);

        Assert.Equal(WorkspaceMemberStatus.Removed, member.Status);
        await _audit.Received(1).AppendAsync(_workspace.Id, AuditTypes.RepoRevokeSkipped, _managerId, Arg.Any<object>());
        await _workspaces.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task ForcedRemoval_WhenGitHubWorks_LeavesNoRecord()
    {
        var member = Member("hbob8686");

        await Remove(member, force: true);

        Assert.Equal(WorkspaceMemberStatus.Removed, member.Status);
        await _audit.DidNotReceiveWithAnyArgs().AppendAsync(default, default!, default, default!);
    }

    [Fact]
    public async Task Removing_WithoutALinkedRepo_NeverCallsGitHub()
    {
        _workspace.GitHubInstallationId = null;
        var member = Member("hbob8686");

        await Remove(member);

        Assert.Equal(WorkspaceMemberStatus.Removed, member.Status);
        await _app.DidNotReceiveWithAnyArgs().RemoveCollaboratorAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task Removing_AMemberWithoutAGitHubName_NeverCallsGitHub()
    {
        var member = Member(null);

        await Remove(member);

        Assert.Equal(WorkspaceMemberStatus.Removed, member.Status);
        await _app.DidNotReceiveWithAnyArgs().RemoveCollaboratorAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task Unlinking_TakesEveryMembersAccessAway_ExceptTheManagers()
    {
        var manager = Member("the-manager", WorkspaceMemberRole.Manager);
        var first = Member("dev-one");
        var second = Member("dev-two");
        _members.GetByWorkspaceIdAsync(_workspace.Id).Returns([manager, first, second]);

        await _service.UnlinkGitHubRepoAsync(_managerId, _workspace.Id);

        await _app.Received(1).RemoveCollaboratorAsync(42, "shipmate-test-org", "demo-app", "dev-one");
        await _app.Received(1).RemoveCollaboratorAsync(42, "shipmate-test-org", "demo-app", "dev-two");
        await _app.DidNotReceive().RemoveCollaboratorAsync(42, "shipmate-test-org", "demo-app", "the-manager");
        Assert.Null(_workspace.GitHubRepoOwner);
    }

    // The repository must not be left with a check nobody reports, nor with locks ShipMate no longer maintains.
    [Fact]
    public async Task Unlinking_TakesShipMatesRulesOffTheRepository()
    {
        await _service.UnlinkGitHubRepoAsync(_managerId, _workspace.Id);

        await _app.Received(1).RemoveRepoLockAsync(42, "shipmate-test-org", "demo-app");
    }

    [Fact]
    public async Task Unlinking_WhenTheCleanupFails_StillUnlinks_AndLeavesARecord()
    {
        _app.RemoveRepoLockAsync(42, "shipmate-test-org", "demo-app")
            .Returns(Task.FromException(new GitHubRequestFailedException()));

        await _service.UnlinkGitHubRepoAsync(_managerId, _workspace.Id);

        Assert.Null(_workspace.GitHubRepoOwner);
        await _audit.Received(1).AppendAsync(_workspace.Id, AuditTypes.RepoCleanupFailed, null, Arg.Any<object>());
    }

    [Fact]
    public async Task Unlinking_AWorkspaceWithoutARepo_NeverCallsGitHub()
    {
        _workspace.GitHubInstallationId = null;

        await _service.UnlinkGitHubRepoAsync(_managerId, _workspace.Id);

        await _app.DidNotReceiveWithAnyArgs().RemoveRepoLockAsync(default, default!, default!);
    }

    [Fact]
    public async Task Unlinking_WhenGitHubFails_KeepsTheLink()
    {
        var dev = Member("dev-one");
        _members.GetByWorkspaceIdAsync(_workspace.Id).Returns([dev]);
        GitHubFails();

        await Assert.ThrowsAsync<GitHubRevokeFailedException>(() => _service.UnlinkGitHubRepoAsync(_managerId, _workspace.Id));

        Assert.Equal("shipmate-test-org", _workspace.GitHubRepoOwner);
        Assert.Equal(42, _workspace.GitHubInstallationId);
    }
}
