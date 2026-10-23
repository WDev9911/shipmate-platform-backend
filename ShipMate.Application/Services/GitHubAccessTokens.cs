using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Services;

/// <summary>Reads a user's usable GitHub access token, shared by everything that calls GitHub on their behalf.</summary>
internal static class GitHubAccessTokens
{
    public static async Task<string> GetValidOrThrowAsync(
        IGitHubConnectionRepository connections, IEncryptionService encryption, Guid userId)
    {
        var connection = await connections.GetByUserIdAsync(userId)
            ?? throw new GitHubNotConnectedException();

        if (connection.AccessTokenExpiresAt is not null && connection.AccessTokenExpiresAt < DateTime.UtcNow)
        {
            // Auto-refreshing via the stored GitHub refresh token is BUILD-phase work — for now,
            // just ask the user to sign in with GitHub again to get a fresh access token.
            throw new GitHubNotConnectedException();
        }

        return encryption.Decrypt(connection.GitHubAccessTokenEncrypted);
    }
}
