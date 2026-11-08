using AutoMapper;
using NSubstitute;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;

namespace ShipMate.Tests.Application;

// GitHub's access tokens last a few hours: an expired one is renewed with the refresh token instead of sending the
// user back through GitHub.
public class GitHubTokenRefreshTests
{
    private readonly IGitHubConnectionRepository _connections = Substitute.For<IGitHubConnectionRepository>();
    private readonly IGitHubOAuthService _oauth = Substitute.For<IGitHubOAuthService>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly WorkspaceService _service;
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly GitHubConnection _connection;

    public GitHubTokenRefreshTests()
    {
        var guard = Substitute.For<IWorkspaceAccessGuard>();
        guard.GetWorkspaceAsManagerAsync(_managerId, _workspaceId).Returns(new Workspace());

        _encryption.Decrypt(Arg.Any<string>()).Returns(call => "plain:" + call.Arg<string>());
        _encryption.Encrypt(Arg.Any<string>()).Returns(call => "enc:" + call.Arg<string>());

        _connection = new GitHubConnection
        {
            UserId = _managerId,
            GitHubAccessTokenEncrypted = "old-access",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(-1),
            RefreshTokenEncrypted = "old-refresh",
            RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        _connections.GetByUserIdAsync(_managerId).Returns(_connection);

        _service = new WorkspaceService(
            _workspaces, Substitute.For<IWorkspaceMemberRepository>(), guard,
            Substitute.For<IProductDefinitionRepository>(), Substitute.For<IUserRepository>(),
            _connections, _oauth, Substitute.For<IGitHubAppService>(), _encryption, Substitute.For<IEmailSender>(),
            Substitute.For<IWorkspaceNotifier>(), Substitute.For<IAuditTrail>(), Substitute.For<IFeatureAssignmentRepository>(), Substitute.For<IMapper>());
    }

    private Task<List<GitHubRepoDto>> ListRepos() => _service.GetAvailableGitHubReposAsync(_managerId, _workspaceId);

    private static GitHubTokenResult Fresh() => new()
    {
        AccessToken = "new-access",
        ExpiresAt = DateTime.UtcNow.AddHours(8),
        RefreshToken = "new-refresh",
        RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(180)
    };

    [Fact]
    public async Task ATokenStillValid_IsUsedAsItIs_WithoutCallingGitHub()
    {
        _connection.AccessTokenExpiresAt = DateTime.UtcNow.AddHours(2);

        await ListRepos();

        await _oauth.DidNotReceiveWithAnyArgs().RefreshTokenAsync(default!);
        await _oauth.Received(1).GetUserRepositoriesAsync("plain:old-access");
    }

    [Fact]
    public async Task AnExpiredToken_IsRenewedWithTheRefreshToken_AndTheNewOneIsUsedAndSaved()
    {
        _oauth.RefreshTokenAsync("plain:old-refresh").Returns(Fresh());

        await ListRepos();

        await _oauth.Received(1).GetUserRepositoriesAsync("new-access");
        Assert.Equal("enc:new-access", _connection.GitHubAccessTokenEncrypted);
        Assert.Equal("enc:new-refresh", _connection.RefreshTokenEncrypted);
        Assert.True(_connection.AccessTokenExpiresAt > DateTime.UtcNow);
        _connections.Received(1).Update(_connection);
        await _workspaces.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task WhenGitHubRefusesTheRefreshToken_TheUserHasToConnectAgain()
    {
        _oauth.RefreshTokenAsync(Arg.Any<string>()).Returns(Task.FromException<GitHubTokenResult>(new InvalidOrExpiredTokenException()));

        await Assert.ThrowsAsync<GitHubNotConnectedException>(ListRepos);

        await _oauth.DidNotReceiveWithAnyArgs().GetUserRepositoriesAsync(default!);
    }

    [Fact]
    public async Task AnExpiredTokenWithoutARefreshToken_MeansConnectingAgain()
    {
        _connection.RefreshTokenEncrypted = null;

        await Assert.ThrowsAsync<GitHubNotConnectedException>(ListRepos);

        await _oauth.DidNotReceiveWithAnyArgs().RefreshTokenAsync(default!);
    }

    [Fact]
    public async Task AnExpiredRefreshToken_MeansConnectingAgain_WithoutAskingGitHub()
    {
        _connection.RefreshTokenExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<GitHubNotConnectedException>(ListRepos);

        await _oauth.DidNotReceiveWithAnyArgs().RefreshTokenAsync(default!);
    }

    [Fact]
    public async Task WhenGitHubCantBeReached_TheErrorSaysSo_NotThatTheUserMustReconnect()
    {
        _oauth.RefreshTokenAsync(Arg.Any<string>()).Returns(Task.FromException<GitHubTokenResult>(new HttpRequestException("down")));

        await Assert.ThrowsAsync<GitHubRequestFailedException>(ListRepos);
    }

    [Fact]
    public async Task ATokenThatNeverExpires_IsUsedAsItIs()
    {
        _connection.AccessTokenExpiresAt = null;

        await ListRepos();

        await _oauth.Received(1).GetUserRepositoriesAsync("plain:old-access");
    }
}
