using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;

namespace ShipMate.Tests.Application;

public class GitHubRepoLinkTests
{
    private const string InstallUrl = "https://github.com/apps/shipmate-bot-dev/installations/new";

    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IGitHubOAuthService _gitHub = Substitute.For<IGitHubOAuthService>();
    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
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

        _service = new WorkspaceService(
            _workspaces, Substitute.For<IWorkspaceMemberRepository>(), _guard,
            Substitute.For<IProductDefinitionRepository>(), Substitute.For<IUserRepository>(),
            connections, _gitHub, _app, encryption, Substitute.For<IEmailSender>(), Substitute.For<IWorkspaceNotifier>(), Substitute.For<IMapper>());

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
