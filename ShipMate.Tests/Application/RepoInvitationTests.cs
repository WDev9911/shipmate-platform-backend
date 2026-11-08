using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class RepoInvitationTests
{
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
    private readonly IAuditTrail _audit = Substitute.For<IAuditTrail>();
    private readonly WorkspaceService _service;
    private readonly Workspace _workspace = new()
    {
        GitHubRepoOwner = "shipmate-test-org",
        GitHubRepoName = "demo-app",
        GitHubInstallationId = 42
    };
    private readonly User _user = new() { GitHubUsername = "hbob8686" };
    private readonly WorkspaceMember _invitation;

    public RepoInvitationTests()
    {
        var mapper = Substitute.For<IMapper>();
        mapper.Map<WorkspaceMemberDto>(Arg.Any<object>()).Returns(_ => new WorkspaceMemberDto());

        _service = new WorkspaceService(
            _workspaces, _members, Substitute.For<IWorkspaceAccessGuard>(),
            Substitute.For<IProductDefinitionRepository>(), Substitute.For<IUserRepository>(),
            Substitute.For<IGitHubConnectionRepository>(), Substitute.For<IGitHubOAuthService>(), _app,
            Substitute.For<IEncryptionService>(), Substitute.For<IEmailSender>(),
            Substitute.For<IWorkspaceNotifier>(), _audit, Substitute.For<IFeatureAssignmentRepository>(), mapper);

        _invitation = new WorkspaceMember
        {
            WorkspaceId = _workspace.Id, UserId = _user.Id, User = _user, Status = WorkspaceMemberStatus.Invited
        };
        _members.GetByWorkspaceAndUserIdAsync(_workspace.Id, _user.Id).Returns(_invitation);
        _workspaces.GetByIdAsync(_workspace.Id).Returns(_workspace);
    }

    private Task<WorkspaceMemberDto> Accept() =>
        _service.AcceptInvitationAsync(_user.Id, _workspace.Id, new AcceptInvitationRequest { SkillLevel = 3 });

    [Fact]
    public async Task Accepting_InvitesTheMemberToTheLinkedRepo()
    {
        _app.InviteCollaboratorAsync(42, "shipmate-test-org", "demo-app", "hbob8686").Returns(true);

        var result = await Accept();

        await _app.Received(1).InviteCollaboratorAsync(42, "shipmate-test-org", "demo-app", "hbob8686");
        Assert.Equal("sent", result.RepoInvite);
    }

    [Fact]
    public async Task Accepting_WhenTheMemberAlreadyHasAccess_SaysSo()
    {
        _app.InviteCollaboratorAsync(42, "shipmate-test-org", "demo-app", "hbob8686").Returns(false);

        Assert.Equal("alreadyHasAccess", (await Accept()).RepoInvite);
    }

    [Fact]
    public async Task WorkspaceWithoutARepo_NeverCallsGitHub()
    {
        _workspace.GitHubRepoOwner = null;
        _workspace.GitHubRepoName = null;

        var result = await Accept();

        await _app.DidNotReceiveWithAnyArgs().InviteCollaboratorAsync(default, default!, default!, default!);
        Assert.Equal("noRepo", result.RepoInvite);
    }

    [Fact]
    public async Task RepoLinkedBeforeTheAppExisted_NeverCallsGitHub()
    {
        _workspace.GitHubInstallationId = null;

        await Accept();

        await _app.DidNotReceiveWithAnyArgs().InviteCollaboratorAsync(default, default!, default!, default!);
    }

    // Members get repository access by their GitHub name, so nobody joins without one.
    [Fact]
    public async Task MemberWithoutAGitHubUsername_CannotAccept()
    {
        _user.GitHubUsername = null;

        await Assert.ThrowsAsync<GitHubRequiredToJoinException>(Accept);

        Assert.Equal(WorkspaceMemberStatus.Invited, _invitation.Status);
        await _app.DidNotReceiveWithAnyArgs().InviteCollaboratorAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task GitHubRefusing_DoesNotStopTheMemberFromJoining_AndIsRecorded()
    {
        _app.InviteCollaboratorAsync(42, "shipmate-test-org", "demo-app", "hbob8686")
            .Returns(Task.FromException<bool>(new GitHubRequestFailedException()));

        var result = await Accept();

        Assert.Equal(WorkspaceMemberStatus.Active, _invitation.Status);
        Assert.Equal("failed", result.RepoInvite);
        await _audit.Received(1).AppendAsync(_workspace.Id, AuditTypes.RepoInviteFailed, null, Arg.Any<object>());
    }

    [Fact]
    public async Task TheAppBeingUninstalled_DoesNotStopTheMemberFromJoiningEither()
    {
        _app.InviteCollaboratorAsync(42, "shipmate-test-org", "demo-app", "hbob8686")
            .Returns(Task.FromException<bool>(new GitHubAppUninstalledException()));

        var result = await Accept();

        Assert.Equal(WorkspaceMemberStatus.Active, _invitation.Status);
        Assert.Equal("failed", result.RepoInvite);
    }

    [Fact]
    public async Task Declining_NeverCallsGitHub()
    {
        await _service.DeclineInvitationAsync(_user.Id, _workspace.Id);

        await _app.DidNotReceiveWithAnyArgs().InviteCollaboratorAsync(default, default!, default!, default!);
        await _app.DidNotReceiveWithAnyArgs().RemoveCollaboratorAsync(default, default!, default!, default!);
    }
}
