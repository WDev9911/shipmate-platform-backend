using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class GitHubRepoLinkTests
{
    private const string InstallUrl = "https://github.com/apps/shipmate-bot-dev/installations/new";

    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IGitHubOAuthService _gitHub = Substitute.For<IGitHubOAuthService>();
    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly Workspace _workspace = new();
    private readonly WorkspaceService _service;
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public GitHubRepoLinkTests()
    {
        var connections = Substitute.For<IGitHubConnectionRepository>();
        connections.GetByUserIdAsync(_managerId).Returns(new GitHubConnection { GitHubAccessTokenEncrypted = "enc" });
        var encryption = Substitute.For<IEncryptionService>();
        encryption.Decrypt("enc").Returns("manager-token");
        encryption.Encrypt(Arg.Any<string>()).Returns("encrypted");

        _members.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([]);
        _assignments.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([]);
        _app.RepoHasCommitsAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        _service = new WorkspaceService(
            _workspaces, _members, _guard,
            Substitute.For<IProductDefinitionRepository>(), Substitute.For<IUserRepository>(),
            connections, _gitHub, _app, encryption, Substitute.For<IEmailSender>(), Substitute.For<IWorkspaceNotifier>(), Substitute.For<IAuditTrail>(), _assignments, Substitute.For<IMapper>());

        _guard.GetWorkspaceAsManagerAsync(_managerId, _workspaceId).Returns(_workspace);
        _gitHub.GetUserRepositoriesAsync("manager-token")
            .Returns([new GitHubRepoDto { Owner = "clinic", Name = "booking", DefaultBranch = "main" }]);
        _app.InstallUrl.Returns(InstallUrl);
    }

    private Task<WorkspaceDto> Link(string owner = "clinic", string name = "booking") =>
        _service.LinkGitHubRepoAsync(_managerId, _workspaceId, new LinkGitHubRepoRequest { Owner = owner, Name = name });

    [Fact]
    public async Task Link_AppInstalledOnAnOrganizationRepo_StoresTheInstallation()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));

        await Link();

        Assert.Equal("clinic", _workspace.GitHubRepoOwner);
        Assert.Equal("booking", _workspace.GitHubRepoName);
        Assert.Equal(4242, _workspace.GitHubInstallationId);
        Assert.True(_workspace.GitHubRepoIsOrganization);
        await _workspaces.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Link_AppInstalledOnAPersonalRepo_RemembersItCannotBeHardLocked()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(7, IsOrganization: false));

        await Link();

        Assert.Equal(7, _workspace.GitHubInstallationId);
        Assert.False(_workspace.GitHubRepoIsOrganization);
    }

    [Fact]
    public async Task Link_AppNotInstalled_IsRejectedWithTheInstallLinkAndNothingIsStored()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns((GitHubAppInstallation?)null);

        var error = await Assert.ThrowsAsync<GitHubAppNotInstalledException>(() => Link());

        Assert.Equal("GITHUB_APP_NOT_INSTALLED", error.ErrorCode);
        Assert.Equal(409, error.StatusCode);
        Assert.Equal(InstallUrl, error.Details!.GetType().GetProperty("InstallUrl")!.GetValue(error.Details));
        Assert.Null(_workspace.GitHubRepoOwner);
        Assert.Null(_workspace.GitHubInstallationId);
        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task Link_RepoTheManagerCannotReach_IsRejectedBeforeAskingAboutTheApp()
    {
        await Assert.ThrowsAsync<GitHubRepoNotAccessibleException>(() => Link("someone-else", "private-repo"));

        await _app.DidNotReceiveWithAnyArgs().FindInstallationAsync(default!, default!);
    }

    [Fact]
    public async Task Unlink_ClearsTheInstallationToo()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "booking";
        _workspace.GitHubInstallationId = 4242;
        _workspace.GitHubRepoIsOrganization = true;

        await _service.UnlinkGitHubRepoAsync(_managerId, _workspaceId);

        Assert.Null(_workspace.GitHubRepoOwner);
        Assert.Null(_workspace.GitHubInstallationId);
        Assert.False(_workspace.GitHubRepoIsOrganization);
    }

    [Fact]
    public async Task Unlink_AlsoForgetsTheMergeGateAndTheAuditBookmark()
    {
        _workspace.GitHubMergeGateEnabled = true;
        _workspace.LastAuditedMainSha = new string('a', 40);

        await _service.UnlinkGitHubRepoAsync(_managerId, _workspaceId);

        Assert.False(_workspace.GitHubMergeGateEnabled);
        Assert.Null(_workspace.LastAuditedMainSha);
    }

    private WorkspaceMember ActiveMember(string? gitHubName, WorkspaceMemberRole role = WorkspaceMemberRole.Developer) =>
        new() { Role = role, Status = WorkspaceMemberStatus.Active, User = new User { GitHubUsername = gitHubName } };

    // The repository usually comes after the team: the people already in get invited when it is linked.
    [Fact]
    public async Task Link_InvitesTheMembersWhoJoinedBefore_ButNotTheManagerOrThoseWithoutAGitHubName()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _members.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([
            ActiveMember("the-manager", WorkspaceMemberRole.Manager),
            ActiveMember("dev-one"),
            ActiveMember(null),
            new WorkspaceMember { Status = WorkspaceMemberStatus.Invited, User = new User { GitHubUsername = "not-yet" } }
        ]);

        await Link();

        await _app.Received(1).InviteCollaboratorAsync(4242, "clinic", "booking", "dev-one");
        await _app.ReceivedWithAnyArgs(1).InviteCollaboratorAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task Link_ToAnotherRepo_TakesTheMembersOffTheOldOne()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "old-booking";
        _workspace.GitHubInstallationId = 1111;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _members.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([
            ActiveMember("the-manager", WorkspaceMemberRole.Manager),
            ActiveMember("dev-one")
        ]);

        await Link();

        await _app.Received(1).RemoveCollaboratorAsync(1111, "clinic", "old-booking", "dev-one");
        await _app.DidNotReceive().RemoveCollaboratorAsync(1111, "clinic", "old-booking", "the-manager");
        await _app.Received(1).InviteCollaboratorAsync(4242, "clinic", "booking", "dev-one");
    }

    [Fact]
    public async Task Link_ToAnotherRepo_TakesShipMatesRulesOffTheOldOne()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "old-booking";
        _workspace.GitHubInstallationId = 1111;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));

        await Link();

        await _app.Received(1).RemoveRepoLockAsync(1111, "clinic", "old-booking");
    }

    [Fact]
    public async Task Link_TheSameRepoAgain_LeavesItsRulesAlone()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "booking";
        _workspace.GitHubInstallationId = 4242;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));

        await Link();

        await _app.DidNotReceiveWithAnyArgs().RemoveRepoLockAsync(default, default!, default!);
    }

    [Fact]
    public async Task Link_ToAnotherRepo_WhenTheOldOneCantBeLeft_ChangesNothing()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "old-booking";
        _workspace.GitHubInstallationId = 1111;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _members.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([ActiveMember("dev-one")]);
        _app.RemoveCollaboratorAsync(1111, "clinic", "old-booking", "dev-one")
            .Returns(Task.FromException(new GitHubRequestFailedException()));

        await Assert.ThrowsAsync<GitHubRevokeFailedException>(() => Link());

        Assert.Equal("old-booking", _workspace.GitHubRepoName);
        Assert.Equal(1111, _workspace.GitHubInstallationId);
    }

    [Fact]
    public async Task Link_TheSameRepoAgain_TakesNobodyOff()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "booking";
        _workspace.GitHubInstallationId = 4242;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _members.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([ActiveMember("dev-one")]);

        await Link();

        await _app.DidNotReceiveWithAnyArgs().RemoveCollaboratorAsync(default, default!, default!, default!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Link_RemembersWhetherTheMergeGateCouldBeSwitchedOn(bool gateOn)
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _app.EnsureMergeGateAsync(4242, true, "clinic", "booking").Returns(gateOn);

        await Link();

        Assert.Equal(gateOn, _workspace.GitHubMergeGateEnabled);
    }

    // Nothing to protect in a repo without commits: the gate would stay off with no way to switch it on later.
    [Fact]
    public async Task Link_ARepoWithoutCommits_IsRejectedAndNothingIsChanged()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _app.RepoHasCommitsAsync(4242, "clinic", "booking").Returns(false);

        await Assert.ThrowsAsync<GitHubRepoEmptyException>(() => Link());

        Assert.Null(_workspace.GitHubInstallationId);
        await _app.DidNotReceiveWithAnyArgs().EnsureMergeGateAsync(default, default, default!, default!);
        await _workspaces.DidNotReceive().SaveChangesAsync();
    }

    private static FeatureAssignment Held(string branch, bool released = false, bool branchCreated = true) => new()
    {
        FeatureId = Guid.NewGuid(),
        AssignedUserId = Guid.NewGuid(),
        BranchName = branch,
        BranchCreatedAt = branchCreated ? DateTime.UtcNow : null,
        ReleasedAt = released ? DateTime.UtcNow : null,
        LastApprovedSha = "old-approved",
        FlaggedHeadSha = "old-flagged"
    };

    // Unlinking took the rules off, so a link that comes back has to put them back on the branches already handed out.
    [Fact]
    public async Task Link_LocksTheBranchesAlreadyHeldAgain_ButNotReleasedOnesOrOnesNeverCreated()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _assignments.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([
            Held("feature/a-dev"), Held("feature/b-dev"), Held("feature/released", released: true), Held("feature/never", branchCreated: false)
        ]);

        await Link();

        await _app.Received(1).CreateProtectedBranchAsync(4242, true, "clinic", "booking", "feature/a-dev");
        await _app.Received(1).CreateProtectedBranchAsync(4242, true, "clinic", "booking", "feature/b-dev");
        await _app.Received(2).CreateProtectedBranchAsync(4242, true, "clinic", "booking", Arg.Any<string>());
    }

    [Fact]
    public async Task Link_WhenOneBranchCannotBeLocked_StillLinks_AndMarksThatBranchSoItsHolderCanRetry()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        var failing = Held("feature/a-dev");
        var fine = Held("feature/b-dev");
        _assignments.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([failing, fine]);
        _app.CreateProtectedBranchAsync(4242, true, "clinic", "booking", "feature/a-dev")
            .Returns(Task.FromException(new GitHubBranchCreationFailedException()));

        await Link();

        Assert.Equal("clinic", _workspace.GitHubRepoOwner);
        Assert.Null(failing.BranchCreatedAt);
        Assert.NotNull(fine.BranchCreatedAt);
        await _assignments.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Link_TheSameRepoAgain_KeepsWhatWasApprovedOnTheBranches()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "booking";
        _workspace.GitHubInstallationId = 4242;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        var held = Held("feature/a-dev");
        _assignments.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([held]);

        await Link();

        Assert.Equal("old-approved", held.LastApprovedSha);
        Assert.Equal("old-flagged", held.FlaggedHeadSha);
    }

    // The branch in a new repository starts from that repository's default branch, so the old approvals no longer describe it.
    [Fact]
    public async Task Link_ToAnotherRepo_LocksTheBranchesThereAndForgetsWhatWasApprovedInTheOldOne()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "old-booking";
        _workspace.GitHubInstallationId = 1111;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        var held = Held("feature/a-dev");
        _assignments.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([held]);

        await Link();

        await _app.Received(1).CreateProtectedBranchAsync(4242, true, "clinic", "booking", "feature/a-dev");
        Assert.Null(held.LastApprovedSha);
        Assert.Null(held.FlaggedHeadSha);
    }

    // The old repository is only let go of once the new one is ready: a refusal there must leave the old one locked.
    [Fact]
    public async Task Link_ToAnotherRepo_WhenTheNewOneRefusesTheMergeGate_TouchesNothingOnTheOldOne()
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "old-booking";
        _workspace.GitHubInstallationId = 1111;
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _members.GetByWorkspaceIdAsync(Arg.Any<Guid>()).Returns([ActiveMember("the-manager", WorkspaceMemberRole.Manager), ActiveMember("dev-one")]);
        _app.EnsureMergeGateAsync(4242, true, "clinic", "booking")
            .Returns(Task.FromException<bool>(new GitHubRequestFailedException()));

        await Assert.ThrowsAsync<GitHubRequestFailedException>(() => Link());

        await _app.DidNotReceiveWithAnyArgs().RemoveRepoLockAsync(default, default!, default!);
        await _app.DidNotReceiveWithAnyArgs().RemoveCollaboratorAsync(default, default!, default!, default!);
        Assert.Equal("old-booking", _workspace.GitHubRepoName);
        Assert.Equal(1111, _workspace.GitHubInstallationId);
    }

    [Fact]
    public async Task Link_WhenGitHubRefusesTheMergeGate_LinksNothing()
    {
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(4242, IsOrganization: true));
        _app.EnsureMergeGateAsync(4242, true, "clinic", "booking")
            .Returns(Task.FromException<bool>(new GitHubRequestFailedException()));

        await Assert.ThrowsAsync<GitHubRequestFailedException>(() => Link());

        await _workspaces.DidNotReceive().SaveChangesAsync();
    }
}
