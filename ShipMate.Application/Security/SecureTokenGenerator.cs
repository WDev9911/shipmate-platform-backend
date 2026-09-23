using System.Security.Cryptography;
using System.Text;

namespace ShipMate.Application.Security;

/// <summary>
/// Generates random tokens (email verification, password reset, refresh token...).
/// Only the SHA-256 hash is persisted — the plaintext only ever appears in the email/response, never stored.
/// </summary>
public static class SecureTokenGenerator
{
    public static (string PlainToken, string TokenHash) GenerateTokenPair()
    {
        var plainToken = ToBase64Url(RandomNumberGenerator.GetBytes(32));
        return (plainToken, Hash(plainToken));
    }

    /// <summary>
    /// Generates a numeric OTP code (e.g. for email verification) using an unbiased random range.
    /// </summary>
    public static (string Code, string CodeHash) GenerateNumericCode(int length = 6)
    {
        var max = (int)Math.Pow(10, length);
        var code = RandomNumberGenerator.GetInt32(0, max).ToString().PadLeft(length, '0');
        return (code, Hash(code));
    }

    public static string Hash(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes);
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
