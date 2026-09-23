using ShipMate.Application.DTOs.Auth;

namespace ShipMate.Application.Interfaces.Services;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request);
    Task<AuthResponse> VerifyEmailAsync(VerifyEmailRequest request);
    Task ResendVerificationAsync(ResendVerificationRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? deviceInfo);
    Task<AuthResponse> GitHubLoginAsync(string code, string? deviceInfo);
    Task ConnectGitHubAsync(Guid userId, string code);
    Task DisconnectGitHubAsync(Guid userId);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<VerifyResetCodeResponse> VerifyResetCodeAsync(VerifyResetCodeRequest request);
    Task ResetPasswordAsync(ResetPasswordRequest request);
}
