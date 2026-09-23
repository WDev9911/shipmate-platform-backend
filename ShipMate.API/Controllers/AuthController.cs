using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Security;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private const string GitHubStateCookieName = "githubOAuthState";

    private readonly IAuthService _authService;
    private readonly IOAuthHandoffStore _oauthHandoffStore;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IOAuthHandoffStore oauthHandoffStore, IConfiguration configuration)
    {
        _authService = authService;
        _oauthHandoffStore = oauthHandoffStore;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        await _authService.RegisterAsync(request);
        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Registration successful. Please check your email for the verification code."
        });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var result = await _authService.VerifyEmailAsync(request);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(result);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request)
    {
        await _authService.ResendVerificationAsync(request);
        // Always the same response, to avoid revealing whether the account exists or is already verified.
        return Ok(new { message = "If an unverified account with that email exists, a new code has been sent." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var deviceInfo = Request.Headers.UserAgent.ToString();
        var result = await _authService.LoginAsync(request, deviceInfo);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(result);
    }

    [HttpGet("github/login-url")]
    public IActionResult GetGitHubLoginUrl()
    {
        var (state, _) = SecureTokenGenerator.GenerateTokenPair();

        Response.Cookies.Append(GitHubStateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
            Path = "/api/auth/github"
        });

        var clientId = _configuration["GitHub:ClientId"];
        var redirectUri = _configuration["GitHub:RedirectUri"];
        var scope = Uri.EscapeDataString("read:user user:email");

        var url = "https://github.com/login/oauth/authorize"
            + $"?client_id={clientId}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri!)}"
            + $"&scope={scope}"
            + $"&state={state}";

        return Ok(new { url });
    }

    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback([FromQuery] string code, [FromQuery] string state)
    {
        var frontendUrl = _configuration["App:FrontendBaseUrl"] ?? "http://localhost:3000";

        var cookieState = Request.Cookies[GitHubStateCookieName];
        Response.Cookies.Delete(GitHubStateCookieName, new CookieOptions { Path = "/api/auth/github" });

        if (string.IsNullOrEmpty(cookieState) || cookieState != state)
        {
            return Redirect($"{frontendUrl}/login?error=invalid_state");
        }

        try
        {
            var deviceInfo = Request.Headers.UserAgent.ToString();
            var result = await _authService.GitHubLoginAsync(code, deviceInfo);
            var handoffCode = _oauthHandoffStore.Create(result);
            return Redirect($"{frontendUrl}/oauth-callback?handoff={handoffCode}");
        }
        catch (AppException ex)
        {
            return Redirect($"{frontendUrl}/login?error={ex.ErrorCode}");
        }
    }

    [HttpPost("github/exchange")]
    public IActionResult ExchangeGitHubHandoff(GitHubExchangeRequest request)
    {
        var result = _oauthHandoffStore.Consume(request.HandoffCode);
        if (result is null)
        {
            throw new InvalidOrExpiredTokenException();
        }

        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new InvalidOrExpiredTokenException();
        }

        var result = await _authService.RefreshTokenAsync(refreshToken);
        SetRefreshTokenCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authService.LogoutAsync(refreshToken);
        }

        ClearRefreshTokenCookie();
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request);
        // Always the same response whether or not the email exists, to avoid user enumeration.
        return Ok(new { message = "If an account with that email exists, a reset code has been sent." });
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode(VerifyResetCodeRequest request)
    {
        var result = await _authService.VerifyResetCodeAsync(request);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok(new { message = "Password reset successfully. Please log in again." });
    }

    private void SetRefreshTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = expiresAt,
            Path = "/api/auth"
        });
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions { Path = "/api/auth" });
    }
}
