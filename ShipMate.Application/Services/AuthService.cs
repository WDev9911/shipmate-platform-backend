using AutoMapper;
using Microsoft.Extensions.Configuration;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Security;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IEmailVerificationTokenRepository _emailVerificationTokenRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IGitHubConnectionRepository _gitHubConnectionRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailSender _emailSender;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IGitHubOAuthService _gitHubOAuthService;
    private readonly IEncryptionService _encryptionService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        IEmailVerificationTokenRepository emailVerificationTokenRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IGitHubConnectionRepository gitHubConnectionRepository,
        IWorkspaceRepository workspaceRepository,
        IPasswordHasher passwordHasher,
        IEmailSender emailSender,
        IJwtTokenService jwtTokenService,
        IGitHubOAuthService gitHubOAuthService,
        IEncryptionService encryptionService,
        IMapper mapper,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _emailVerificationTokenRepository = emailVerificationTokenRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _gitHubConnectionRepository = gitHubConnectionRepository;
        _workspaceRepository = workspaceRepository;
        _passwordHasher = passwordHasher;
        _emailSender = emailSender;
        _jwtTokenService = jwtTokenService;
        _gitHubOAuthService = gitHubOAuthService;
        _encryptionService = encryptionService;
        _mapper = mapper;
        _configuration = configuration;
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new EmailAlreadyExistsException();
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            HasPassword = true,
            DisplayName = request.DisplayName,
            IsEmailVerified = false,
            Role = UserRole.Developer,
            Status = UserStatus.Active
        };
        await _userRepository.AddAsync(user);

        var (code, codeHash) = SecureTokenGenerator.GenerateNumericCode();
        var verificationToken = new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        await _emailVerificationTokenRepository.AddAsync(verificationToken);

        await _userRepository.SaveChangesAsync();

        await _emailSender.SendAsync(
            user.Email,
            "Verify your ShipMate account",
            $"""
             <p>Hi {user.DisplayName},</p>
             <p>Your verification code is:</p>
             <h2>{code}</h2>
             <p>This code expires in 15 minutes.</p>
             """);
    }

    public async Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new InvalidOrExpiredTokenException();

        var codeHash = SecureTokenGenerator.Hash(request.Code);
        var verificationToken = await _emailVerificationTokenRepository.GetValidTokenAsync(user.Id, codeHash);

        if (verificationToken is null || verificationToken.UsedAt is not null || verificationToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOrExpiredTokenException();
        }

        user.IsEmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        verificationToken.UsedAt = DateTime.UtcNow;

        return await IssueTokensAsync(user, deviceInfo: null);
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        // Same non-revealing behavior as ForgotPassword: silently no-op if the account doesn't
        // exist or is already verified — resending would be meaningless or would leak status.
        if (user is null || user.IsEmailVerified)
        {
            return;
        }

        // Invalidate any still-unused code so only the newest one can ever be valid.
        await _emailVerificationTokenRepository.InvalidateAllUnusedForUserAsync(user.Id);

        var (code, codeHash) = SecureTokenGenerator.GenerateNumericCode();
        var verificationToken = new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        await _emailVerificationTokenRepository.AddAsync(verificationToken);

        await _userRepository.SaveChangesAsync();

        await _emailSender.SendAsync(
            user.Email,
            "Verify your ShipMate account",
            $"""
             <p>Hi {user.DisplayName},</p>
             <p>Your new verification code is:</p>
             <h2>{code}</h2>
             <p>This code expires in 15 minutes.</p>
             """);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? deviceInfo)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null || user.PasswordHash is null)
        {
            throw new InvalidCredentialsException();
        }

        if (user.Status != UserStatus.Active)
        {
            throw new AccountLockedException();
        }

        if (!user.IsEmailVerified)
        {
            throw new EmailNotVerifiedException();
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        return await IssueTokensAsync(user, deviceInfo);
    }

    public async Task<AuthResponse> GitHubLoginAsync(string code, string? deviceInfo)
    {
        var githubToken = await _gitHubOAuthService.ExchangeCodeAsync(code);
        var profile = await _gitHubOAuthService.GetUserProfileAsync(githubToken.AccessToken);

        if (string.IsNullOrEmpty(profile.Email))
        {
            throw new GitHubEmailUnavailableException();
        }

        var user = await _userRepository.GetByGitHubIdAsync(profile.GitHubId);

        if (user is null)
        {
            var existingByEmail = await _userRepository.GetByEmailAsync(profile.Email);

            if (existingByEmail is null)
            {
                // Case 1: brand new user, created entirely from the GitHub profile.
                user = new User
                {
                    Email = profile.Email,
                    DisplayName = profile.Name ?? profile.Username,
                    AvatarUrl = profile.AvatarUrl,
                    HasPassword = false,
                    PasswordHash = null,
                    IsEmailVerified = true,
                    EmailVerifiedAt = DateTime.UtcNow,
                    GitHubId = profile.GitHubId,
                    GitHubUsername = profile.Username,
                    Role = UserRole.Developer,
                    Status = UserStatus.Active
                };
                await _userRepository.AddAsync(user);
            }
            else if (existingByEmail.IsEmailVerified)
            {
                // Case 2: a verified email/password account already owns this email — link GitHub to it.
                existingByEmail.GitHubId = profile.GitHubId;
                existingByEmail.GitHubUsername = profile.Username;
                existingByEmail.UpdatedAt = DateTime.UtcNow;
                _userRepository.Update(existingByEmail);
                user = existingByEmail;

                await _emailSender.SendAsync(
                    user.Email,
                    "Your GitHub account has been linked",
                    $"""
                     <p>Hi {user.DisplayName},</p>
                     <p>Your ShipMate account was just linked to GitHub (@{profile.Username}).</p>
                     <p>If this wasn't you, please secure your account immediately.</p>
                     """);
            }
            else
            {
                // Case 3: the email exists but was never verified — refuse to auto-link.
                throw new EmailNotVerifiedCannotLinkException();
            }

            await _userRepository.SaveChangesAsync();
        }
        else
        {
            // Case 4: returning GitHub user — keep the cached profile fields fresh.
            user.GitHubUsername = profile.Username;
            user.AvatarUrl = profile.AvatarUrl;
            user.UpdatedAt = DateTime.UtcNow;
            _userRepository.Update(user);
        }

        await UpsertGitHubConnectionAsync(user.Id, githubToken);
        await _userRepository.SaveChangesAsync();

        return await IssueTokensAsync(user, deviceInfo);
    }

    public async Task ConnectGitHubAsync(Guid userId, string code)
    {
        var githubToken = await _gitHubOAuthService.ExchangeCodeAsync(code);
        var profile = await _gitHubOAuthService.GetUserProfileAsync(githubToken.AccessToken);

        var owner = await _userRepository.GetByGitHubIdAsync(profile.GitHubId);
        if (owner is not null && owner.Id != userId)
        {
            throw new GitHubAccountAlreadyLinkedException();
        }

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        user.GitHubId = profile.GitHubId;
        user.GitHubUsername = profile.Username;
        user.AvatarUrl = profile.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        await UpsertGitHubConnectionAsync(userId, githubToken);
        await _userRepository.SaveChangesAsync();

        await _emailSender.SendAsync(
            user.Email,
            "Your GitHub account has been linked",
            $"""
             <p>Hi {user.DisplayName},</p>
             <p>Your ShipMate account was just linked to GitHub (@{profile.Username}).</p>
             <p>If this wasn't you, please secure your account immediately.</p>
             """);
    }

    public async Task DisconnectGitHubAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!user.HasPassword)
        {
            throw new GitHubDisconnectRequiresPasswordException();
        }

        if (await _workspaceRepository.HasManagedWorkspaceWithLinkedGitHubRepoAsync(userId))
        {
            throw new GitHubDisconnectBlockedByLinkedWorkspacesException();
        }

        user.GitHubId = null;
        user.GitHubUsername = null;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        var connection = await _gitHubConnectionRepository.GetByUserIdAsync(userId);
        if (connection is not null)
        {
            _gitHubConnectionRepository.Remove(connection);
        }

        await _userRepository.SaveChangesAsync();
    }

    private async Task UpsertGitHubConnectionAsync(Guid userId, GitHubTokenResult githubToken)
    {
        var encryptedAccessToken = _encryptionService.Encrypt(githubToken.AccessToken);
        var encryptedRefreshToken = githubToken.RefreshToken is not null
            ? _encryptionService.Encrypt(githubToken.RefreshToken)
            : null;

        var connection = await _gitHubConnectionRepository.GetByUserIdAsync(userId);
        if (connection is null)
        {
            connection = new GitHubConnection
            {
                UserId = userId,
                GitHubAccessTokenEncrypted = encryptedAccessToken,
                AccessTokenExpiresAt = githubToken.ExpiresAt,
                RefreshTokenEncrypted = encryptedRefreshToken,
                RefreshTokenExpiresAt = githubToken.RefreshTokenExpiresAt,
                Scope = githubToken.Scope,
                ConnectedAt = DateTime.UtcNow
            };
            await _gitHubConnectionRepository.AddAsync(connection);
        }
        else
        {
            connection.GitHubAccessTokenEncrypted = encryptedAccessToken;
            connection.AccessTokenExpiresAt = githubToken.ExpiresAt;
            connection.RefreshTokenEncrypted = encryptedRefreshToken;
            connection.RefreshTokenExpiresAt = githubToken.RefreshTokenExpiresAt;
            connection.Scope = githubToken.Scope;
            connection.RevokedAt = null;
            _gitHubConnectionRepository.Update(connection);
        }
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var tokenHash = SecureTokenGenerator.Hash(refreshToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        if (existingToken is null || existingToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOrExpiredTokenException();
        }

        if (existingToken.RevokedAt is not null)
        {
            // A revoked token being reused is a sign of theft — kill every active session for this user.
            var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(existingToken.UserId);
            foreach (var token in activeTokens)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
            await _userRepository.SaveChangesAsync();

            throw new InvalidOrExpiredTokenException();
        }

        var user = await _userRepository.GetByIdAsync(existingToken.UserId)
            ?? throw new InvalidOrExpiredTokenException();

        return await IssueTokensAsync(user, existingToken.DeviceInfo, tokenToRotate: existingToken);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var tokenHash = SecureTokenGenerator.Hash(refreshToken);
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash);

        // Logout is idempotent: a missing/already-revoked token is not an error, it just means
        // this session is already dead — the client's intent (be logged out) is already satisfied.
        if (existingToken is not null && existingToken.RevokedAt is null)
        {
            existingToken.RevokedAt = DateTime.UtcNow;
            await _userRepository.SaveChangesAsync();
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);

        // Always behave the same way whether the email doesn't exist, or exists but is GitHub-only
        // (no password to reset) — this both avoids user enumeration and enforces that GitHub-only
        // accounts can't gain a password through this flow.
        if (user is null || !user.HasPassword)
        {
            return;
        }

        var (code, codeHash) = SecureTokenGenerator.GenerateNumericCode();
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };
        await _passwordResetTokenRepository.AddAsync(resetToken);
        await _userRepository.SaveChangesAsync();

        await _emailSender.SendAsync(
            user.Email,
            "Reset your ShipMate password",
            $"""
             <p>Hi {user.DisplayName},</p>
             <p>Your password reset code is:</p>
             <h2>{code}</h2>
             <p>This code expires in 15 minutes. If you didn't request this, you can ignore this email.</p>
             """);
    }

    public async Task<VerifyResetCodeResponse> VerifyResetCodeAsync(VerifyResetCodeRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new InvalidOrExpiredTokenException();

        var codeHash = SecureTokenGenerator.Hash(request.Code);
        var resetToken = await _passwordResetTokenRepository.GetValidTokenAsync(user.Id, codeHash);

        if (resetToken is null || resetToken.UsedAt is not null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOrExpiredTokenException();
        }

        // The OTP is verified but not consumed yet — issue a high-entropy session ticket instead.
        // The FE uses this ticket for the final reset-password call, so the user never re-types the code.
        var (ticketPlain, ticketHash) = SecureTokenGenerator.GenerateTokenPair();
        resetToken.VerifiedAt = DateTime.UtcNow;
        resetToken.SessionTicketHash = ticketHash;

        await _userRepository.SaveChangesAsync();

        return new VerifyResetCodeResponse { ResetTicket = ticketPlain };
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var ticketHash = SecureTokenGenerator.Hash(request.ResetTicket);
        var resetToken = await _passwordResetTokenRepository.GetBySessionTicketHashAsync(ticketHash);

        if (resetToken is null || resetToken.VerifiedAt is null || resetToken.UsedAt is not null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new InvalidOrExpiredTokenException();
        }

        var user = await _userRepository.GetByIdAsync(resetToken.UserId)
            ?? throw new InvalidOrExpiredTokenException();

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.HasPassword = true;
        user.UpdatedAt = DateTime.UtcNow;
        _userRepository.Update(user);

        resetToken.UsedAt = DateTime.UtcNow;

        // Changing the password is a security-sensitive event: force re-login on every device.
        var activeTokens = await _refreshTokenRepository.GetActiveTokensByUserIdAsync(user.Id);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _userRepository.SaveChangesAsync();
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, string? deviceInfo, RefreshToken? tokenToRotate = null)
    {
        var (accessToken, accessTokenExpiresAt) = _jwtTokenService.GenerateAccessToken(user);

        var (refreshTokenPlain, refreshTokenHash) = SecureTokenGenerator.GenerateTokenPair();
        var refreshTokenExpiryDays = int.Parse(_configuration["RefreshToken:ExpiryDays"] ?? "14");
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays);

        var newRefreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = refreshTokenExpiresAt,
            DeviceInfo = deviceInfo
        };
        await _refreshTokenRepository.AddAsync(newRefreshToken);

        if (tokenToRotate is not null)
        {
            tokenToRotate.RevokedAt = DateTime.UtcNow;
            tokenToRotate.ReplacedByTokenId = newRefreshToken.Id;
        }

        await _userRepository.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = refreshTokenPlain,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            User = _mapper.Map<UserDto>(user)
        };
    }
}
