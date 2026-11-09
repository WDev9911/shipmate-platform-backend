using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Services;

/// <summary>Reads a user's usable GitHub access token, shared by everything that calls GitHub on their behalf.</summary>
internal static class GitHubAccessTokens
{
    /// <param name="save">Saves the refreshed token. The repositories share one unit of work, so any of their saves does it.</param>
    public static async Task<string> GetValidOrThrowAsync(
        IGitHubConnectionRepository connections, IGitHubOAuthService oauth, IEncryptionService encryption,
        Func<Task> save, Guid userId)
    {
        var connection = await connections.GetByUserIdAsync(userId)
            ?? throw new GitHubNotConnectedException();

        if (connection.AccessTokenExpiresAt is null || connection.AccessTokenExpiresAt > DateTime.UtcNow)
        {
            return encryption.Decrypt(connection.GitHubAccessTokenEncrypted);
        }

        // GitHub's access tokens last a few hours. The refresh token keeps the user connected without sending them back
        // through GitHub; without a usable one they have to connect again.
        var refreshExpired = connection.RefreshTokenExpiresAt is not null && connection.RefreshTokenExpiresAt <= DateTime.UtcNow;
        if (connection.RefreshTokenEncrypted is null || refreshExpired)
        {
            throw new GitHubNotConnectedException();
        }

        GitHubTokenResult fresh;
        try
        {
            fresh = await oauth.RefreshTokenAsync(encryption.Decrypt(connection.RefreshTokenEncrypted));
        }
        catch (InvalidOrExpiredTokenException)
        {
            throw new GitHubNotConnectedException();
        }
        catch (HttpRequestException)
        {
            throw new GitHubRequestFailedException();
        }

        connection.GitHubAccessTokenEncrypted = encryption.Encrypt(fresh.AccessToken);
        connection.AccessTokenExpiresAt = fresh.ExpiresAt;
        connection.RefreshTokenEncrypted = fresh.RefreshToken is null ? null : encryption.Encrypt(fresh.RefreshToken);
        connection.RefreshTokenExpiresAt = fresh.RefreshTokenExpiresAt;
        connections.Update(connection);
        await save();

        return fresh.AccessToken;
    }
}
