using System.Text.RegularExpressions;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Security;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

// Register, email OTP, login, refresh token rotation, password reset and GitHub sign-in.
public class AuthServiceTests
{
    private const string Email = "dev@example.com";
    private const string Password = "Demo@12345";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailVerificationTokenRepository _verificationTokens = Substitute.For<IEmailVerificationTokenRepository>();
    private readonly IPasswordResetTokenRepository _resetTokens = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IGitHubConnectionRepository _gitHubConnections = Substitute.For<IGitHubConnectionRepository>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IEmailSender _emails = Substitute.For<IEmailSender>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IGitHubOAuthService _gitHub = Substitute.For<IGitHubOAuthService>();
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly AuthService _service;

    private readonly List<RefreshToken> _issuedRefreshTokens = new();
    private string? _lastEmailBody;

    public AuthServiceTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(1) == $"hash:{call.ArgAt<string>(0)}");
        _jwt.GenerateAccessToken(Arg.Any<User>()).Returns(("access-token", DateTime.UtcNow.AddMinutes(30)));
        _refreshTokens.AddAsync(Arg.Do<RefreshToken>(t => _issuedRefreshTokens.Add(t)));
        _emails.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(body => _lastEmailBody = body));
        _encryption.Encrypt(Arg.Any<string>()).Returns(call => $"enc:{call.Arg<string>()}");
        var configuration = Substitute.For<IConfiguration>();
        configuration["RefreshToken:ExpiryDays"].Returns("14");

        _service = new AuthService(_users, _verificationTokens, _resetTokens, _refreshTokens, _gitHubConnections,
            _workspaces, _hasher, _emails, _jwt, _gitHub, _encryption, Substitute.For<IMapper>(), configuration);
    }

    private User ExistingUser(bool verified = true, UserStatus status = UserStatus.Active, bool hasPassword = true)
    {
        var user = new User
        {
            Email = Email,
            DisplayName = "Dev",
            PasswordHash = hasPassword ? $"hash:{Password}" : null,
            HasPassword = hasPassword,
            IsEmailVerified = verified,
            Status = status
        };
        _users.GetByEmailAsync(Email).Returns(user);
        _users.GetByIdAsync(user.Id).Returns(user);
        return user;
    }

    private string CodeInLastEmail() => Regex.Match(_lastEmailBody!, @"<h2>(\d+)</h2>").Groups[1].Value;

    // ---------- Register and email verification ----------

    [Fact]
    public async Task Register_CreatesAnUnverifiedDeveloper_AndEmailsACodeThatMatchesTheStoredHash()
    {
        User? created = null;
        EmailVerificationToken? token = null;
        _users.AddAsync(Arg.Do<User>(u => created = u));
        _verificationTokens.AddAsync(Arg.Do<EmailVerificationToken>(t => token = t));

        await _service.RegisterAsync(new RegisterRequest { Email = Email, Password = Password, DisplayName = "Dev" });

        Assert.NotNull(created);
        Assert.False(created.IsEmailVerified);
        Assert.Equal(UserRole.Developer, created.Role);
        Assert.Equal($"hash:{Password}", created.PasswordHash);
        Assert.NotNull(token);
        Assert.Equal(created.Id, token.UserId);
        Assert.Equal(SecureTokenGenerator.Hash(CodeInLastEmail()), token.TokenHash);
        Assert.InRange(token.ExpiresAt, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        await _emails.Received(1).SendAsync(Email, Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Register_EmailAlreadyUsed_IsRejectedAndNoEmailIsSent()
    {
        ExistingUser();

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() =>
            _service.RegisterAsync(new RegisterRequest { Email = Email, Password = Password, DisplayName = "Dev" }));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
        await _emails.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task VerifyEmail_RightCode_VerifiesTheUserUsesTheCodeAndSignsIn()
    {
        var user = ExistingUser(verified: false);
        var token = new EmailVerificationToken { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _verificationTokens.GetValidTokenAsync(user.Id, SecureTokenGenerator.Hash("123456")).Returns(token);

        var response = await _service.VerifyEmailAsync(new VerifyEmailRequest { Email = Email, Code = "123456" });

        Assert.True(user.IsEmailVerified);
        Assert.NotNull(token.UsedAt);
        Assert.Equal("access-token", response.AccessToken);
        var refresh = Assert.Single(_issuedRefreshTokens);
        Assert.Equal(SecureTokenGenerator.Hash(response.RefreshToken), refresh.TokenHash);
    }

    public static TheoryData<string> BadCodes => new() { "wrong", "expired", "used", "unknown-email" };

    [Theory]
    [MemberData(nameof(BadCodes))]
    public async Task VerifyEmail_WrongExpiredUsedOrUnknown_IsRejected(string scenario)
    {
        var user = ExistingUser(verified: false);
        var token = new EmailVerificationToken { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        if (scenario == "expired") token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        if (scenario == "used") token.UsedAt = DateTime.UtcNow.AddMinutes(-1);
        if (scenario != "wrong")
        {
            _verificationTokens.GetValidTokenAsync(user.Id, SecureTokenGenerator.Hash("123456")).Returns(token);
        }

        var email = scenario == "unknown-email" ? "nobody@example.com" : Email;
        await Assert.ThrowsAsync<InvalidOrExpiredTokenException>(() =>
            _service.VerifyEmailAsync(new VerifyEmailRequest { Email = email, Code = "123456" }));

        Assert.False(user.IsEmailVerified);
        Assert.Empty(_issuedRefreshTokens);
    }

    [Fact]
    public async Task ResendVerification_UnverifiedUser_InvalidatesOldCodesAndSendsANewOne()
    {
        var user = ExistingUser(verified: false);

        await _service.ResendVerificationAsync(new ResendVerificationRequest { Email = Email });

        await _verificationTokens.Received(1).InvalidateAllUnusedForUserAsync(user.Id);
        await _verificationTokens.Received(1).AddAsync(Arg.Is<EmailVerificationToken>(t =>
            t.TokenHash == SecureTokenGenerator.Hash(CodeInLastEmail())));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResendVerification_UnknownOrVerifiedEmail_DoesNothingSilently(bool accountExists)
    {
        if (accountExists) ExistingUser(verified: true);

        await _service.ResendVerificationAsync(new ResendVerificationRequest { Email = Email });

        await _emails.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _verificationTokens.DidNotReceive().AddAsync(Arg.Any<EmailVerificationToken>());
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_RightPassword_IssuesTokensForThatDevice()
    {
        ExistingUser();

        var response = await _service.LoginAsync(new LoginRequest { Email = Email, Password = Password }, "Chrome");

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("Chrome", Assert.Single(_issuedRefreshTokens).DeviceInfo);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_GiveTheSameError()
    {
        ExistingUser();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest { Email = Email, Password = "Sai@12345" }, null));
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest { Email = "nobody@example.com", Password = Password }, null));
        Assert.Empty(_issuedRefreshTokens);
    }

    [Fact]
    public async Task Login_GitHubOnlyAccountWithoutPassword_IsRejected()
    {
        ExistingUser(hasPassword: false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _service.LoginAsync(new LoginRequest { Email = Email, Password = Password }, null));
    }

    [Fact]
    public async Task Login_LockedAccount_IsRejected()
    {
        ExistingUser(status: UserStatus.Locked);

        await Assert.ThrowsAsync<AccountLockedException>(() =>
            _service.LoginAsync(new LoginRequest { Email = Email, Password = Password }, null));
    }

    [Fact]
    public async Task Login_EmailNotVerified_IsRejected()
    {
        ExistingUser(verified: false);

        await Assert.ThrowsAsync<EmailNotVerifiedException>(() =>
            _service.LoginAsync(new LoginRequest { Email = Email, Password = Password }, null));
    }

    // ---------- Refresh token and logout ----------

    private RefreshToken StoredRefreshToken(User user, string plain, DateTime? expiresAt = null, DateTime? revokedAt = null)
    {
        var token = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = SecureTokenGenerator.Hash(plain),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(1),
            RevokedAt = revokedAt,
            DeviceInfo = "Chrome"
        };
        _refreshTokens.GetByTokenHashAsync(token.TokenHash).Returns(token);
        return token;
    }

    [Fact]
    public async Task Refresh_ValidToken_IsRotated()
    {
        var user = ExistingUser();
        var old = StoredRefreshToken(user, "old-token");

        var response = await _service.RefreshTokenAsync("old-token");

        var issued = Assert.Single(_issuedRefreshTokens);
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(issued.Id, old.ReplacedByTokenId);
        Assert.Equal("Chrome", issued.DeviceInfo);
        Assert.NotEqual("old-token", response.RefreshToken);
    }

    [Fact]
    public async Task Refresh_UnknownOrExpiredToken_IsRejected()
    {
        var user = ExistingUser();
        StoredRefreshToken(user, "expired", expiresAt: DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<InvalidOrExpiredTokenException>(() => _service.RefreshTokenAsync("expired"));
        await Assert.ThrowsAsync<InvalidOrExpiredTokenException>(() => _service.RefreshTokenAsync("never-issued"));
        Assert.Empty(_issuedRefreshTokens);
    }

    [Fact]
    public async Task Refresh_ReusingARevokedToken_EndsEverySessionOfThatUser()
    {
        var user = ExistingUser();
        StoredRefreshToken(user, "stolen", revokedAt: DateTime.UtcNow.AddMinutes(-5));
        var otherSessions = new List<RefreshToken>
        {
            new() { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddDays(1) },
            new() { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddDays(1) }
        };
        _refreshTokens.GetActiveTokensByUserIdAsync(user.Id).Returns(otherSessions);

        await Assert.ThrowsAsync<InvalidOrExpiredTokenException>(() => _service.RefreshTokenAsync("stolen"));

        Assert.All(otherSessions, t => Assert.NotNull(t.RevokedAt));
        Assert.Empty(_issuedRefreshTokens);
    }

    [Fact]
    public async Task Logout_RevokesTheToken_AndIsSafeToRepeat()
    {
        var user = ExistingUser();
        var token = StoredRefreshToken(user, "session");

        await _service.LogoutAsync("session");
        var revokedAt = token.RevokedAt;
        await _service.LogoutAsync("session");
        await _service.LogoutAsync("never-issued");

        Assert.NotNull(revokedAt);
        Assert.Equal(revokedAt, token.RevokedAt);
        await _users.Received(1).SaveChangesAsync();
    }

    // ---------- Password reset ----------

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ForgotPassword_UnknownOrGitHubOnlyAccount_DoesNothingSilently(bool accountExists, bool hasPassword)
    {
        if (accountExists) ExistingUser(hasPassword: hasPassword);

        await _service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = Email });

        await _resetTokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>());
        await _emails.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ForgotPassword_ExistingAccount_EmailsACodeThatMatchesTheStoredHash()
    {
        ExistingUser();

        await _service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = Email });

        await _resetTokens.Received(1).AddAsync(Arg.Is<PasswordResetToken>(t =>
            t.TokenHash == SecureTokenGenerator.Hash(CodeInLastEmail())));
    }

    [Fact]
    public async Task VerifyResetCode_RightCode_ReturnsATicketWithoutUsingTheCode()
    {
        var user = ExistingUser();
        var token = new PasswordResetToken { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10) };
        _resetTokens.GetValidTokenAsync(user.Id, SecureTokenGenerator.Hash("654321")).Returns(token);

        var response = await _service.VerifyResetCodeAsync(new VerifyResetCodeRequest { Email = Email, Code = "654321" });

        Assert.NotNull(token.VerifiedAt);
        Assert.Null(token.UsedAt);
        Assert.Equal(SecureTokenGenerator.Hash(response.ResetTicket), token.SessionTicketHash);
    }

    [Fact]
    public async Task VerifyResetCode_WrongCode_IsRejected()
    {
        ExistingUser();

        await Assert.ThrowsAsync<InvalidOrExpiredTokenException>(() =>
            _service.VerifyResetCodeAsync(new VerifyResetCodeRequest { Email = Email, Code = "000000" }));
    }

    [Fact]
    public async Task ResetPassword_VerifiedTicket_ChangesThePasswordAndEndsEverySession()
    {
        var user = ExistingUser();
        var token = new PasswordResetToken { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(10), VerifiedAt = DateTime.UtcNow };
        _resetTokens.GetBySessionTicketHashAsync(SecureTokenGenerator.Hash("ticket")).Returns(token);
        var sessions = new List<RefreshToken> { new() { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddDays(1) } };
        _refreshTokens.GetActiveTokensByUserIdAsync(user.Id).Returns(sessions);

        await _service.ResetPasswordAsync(new ResetPasswordRequest { ResetTicket = "ticket", NewPassword = "Moi@12345" });

        Assert.Equal("hash:Moi@12345", user.PasswordHash);
        Assert.NotNull(token.UsedAt);
        Assert.NotNull(Assert.Single(sessions).RevokedAt);
    }

    [Theory]
    [InlineData("not-verified")]
    [InlineData("used")]
    [InlineData("expired")]
    public async Task ResetPassword_TicketNotUsable_IsRejected(string scenario)
    {
        var user = ExistingUser();
        var token = new PasswordResetToken
        {
            UserId = user.Id,
            ExpiresAt = scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(10),
            VerifiedAt = scenario == "not-verified" ? null : DateTime.UtcNow,
            UsedAt = scenario == "used" ? DateTime.UtcNow : null
        };
        _resetTokens.GetBySessionTicketHashAsync(SecureTokenGenerator.Hash("ticket")).Returns(token);

        await Assert.ThrowsAsync<InvalidOrExpiredTokenException>(() =>
            _service.ResetPasswordAsync(new ResetPasswordRequest { ResetTicket = "ticket", NewPassword = "Moi@12345" }));

        Assert.Equal($"hash:{Password}", user.PasswordHash);
    }

    // ---------- GitHub sign-in, connect and disconnect ----------

    private void GitHubReturns(string? email, string gitHubId = "gh-1")
    {
        _gitHub.ExchangeCodeAsync("code").Returns(new GitHubTokenResult { AccessToken = "gh-token", Scope = "repo" });
        _gitHub.GetUserProfileAsync("gh-token").Returns(new GitHubProfileResult
        {
            GitHubId = gitHubId, Username = "dev-gh", Email = email, Name = "Dev GH"
        });
    }

    [Fact]
    public async Task GitHubLogin_NewEmail_CreatesAVerifiedAccountWithoutPassword_AndStoresTheEncryptedToken()
    {
        GitHubReturns("new@example.com");
        User? created = null;
        _users.AddAsync(Arg.Do<User>(u => created = u));

        await _service.GitHubLoginAsync("code", null);

        Assert.NotNull(created);
        Assert.True(created.IsEmailVerified);
        Assert.False(created.HasPassword);
        Assert.Equal("dev-gh", created.GitHubUsername);
        await _gitHubConnections.Received(1).AddAsync(Arg.Is<GitHubConnection>(c =>
            c.UserId == created.Id && c.GitHubAccessTokenEncrypted == "enc:gh-token"));
    }

    [Fact]
    public async Task GitHubLogin_VerifiedAccountWithThatEmail_IsLinkedAndTheOwnerIsWarned()
    {
        var user = ExistingUser();
        GitHubReturns(Email);

        await _service.GitHubLoginAsync("code", null);

        Assert.Equal("gh-1", user.GitHubId);
        await _users.DidNotReceive().AddAsync(Arg.Any<User>());
        await _emails.Received(1).SendAsync(Email, "Your GitHub account has been linked", Arg.Any<string>());
    }

    [Fact]
    public async Task GitHubLogin_UnverifiedAccountWithThatEmail_IsNotLinked()
    {
        var user = ExistingUser(verified: false);
        GitHubReturns(Email);

        await Assert.ThrowsAsync<EmailNotVerifiedCannotLinkException>(() => _service.GitHubLoginAsync("code", null));

        Assert.Null(user.GitHubId);
        Assert.Empty(_issuedRefreshTokens);
    }

    [Fact]
    public async Task GitHubLogin_NoPublicEmail_IsRejected()
    {
        GitHubReturns(email: null);

        await Assert.ThrowsAsync<GitHubEmailUnavailableException>(() => _service.GitHubLoginAsync("code", null));
    }

    [Fact]
    public async Task ConnectGitHub_AccountAlreadyLinkedToSomeoneElse_IsRejected()
    {
        var user = ExistingUser();
        GitHubReturns(Email);
        _users.GetByGitHubIdAsync("gh-1").Returns(new User { Email = "other@example.com" });

        await Assert.ThrowsAsync<GitHubAccountAlreadyLinkedException>(() => _service.ConnectGitHubAsync(user.Id, "code"));

        Assert.Null(user.GitHubId);
    }

    [Fact]
    public async Task DisconnectGitHub_AccountWithoutPassword_IsRejected()
    {
        var user = ExistingUser(hasPassword: false);
        user.GitHubId = "gh-1";

        await Assert.ThrowsAsync<GitHubDisconnectRequiresPasswordException>(() => _service.DisconnectGitHubAsync(user.Id));

        Assert.Equal("gh-1", user.GitHubId);
    }

    [Fact]
    public async Task DisconnectGitHub_ManagerOfAWorkspaceWithALinkedRepo_IsRejected()
    {
        var user = ExistingUser();
        user.GitHubId = "gh-1";
        _workspaces.HasManagedWorkspaceWithLinkedGitHubRepoAsync(user.Id).Returns(true);

        await Assert.ThrowsAsync<GitHubDisconnectBlockedByLinkedWorkspacesException>(() => _service.DisconnectGitHubAsync(user.Id));

        Assert.Equal("gh-1", user.GitHubId);
    }

    [Fact]
    public async Task DisconnectGitHub_ClearsTheLinkAndRemovesTheStoredToken()
    {
        var user = ExistingUser();
        user.GitHubId = "gh-1";
        user.GitHubUsername = "dev-gh";
        var connection = new GitHubConnection { UserId = user.Id };
        _gitHubConnections.GetByUserIdAsync(user.Id).Returns(connection);

        await _service.DisconnectGitHubAsync(user.Id);

        Assert.Null(user.GitHubId);
        Assert.Null(user.GitHubUsername);
        _gitHubConnections.Received(1).Remove(connection);
    }
}
