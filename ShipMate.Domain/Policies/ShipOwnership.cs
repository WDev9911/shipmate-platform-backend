using System.Security.Cryptography;

namespace ShipMate.Domain.Policies;

/// <summary>
/// How a team proves the production URL is theirs: the site serves the workspace's token at a fixed path. Without it,
/// any working website (someone else's included) could be shipped and the Ship Card would prove nothing.
/// </summary>
public static class ShipOwnership
{
    public const string FilePath = "/.well-known/shipmate.txt";

    // Enough to read a token, small enough that a large file can't be pushed at the server.
    public const int MaxFileBytes = 1024;

    private const string Prefix = "shipmate-";

    public static string NewToken() =>
        Prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>
    /// The file proves ownership when one of its lines is exactly the token. Lines let one domain serve the tokens of
    /// several workspaces; surrounding whitespace and a byte order mark are what editors and hosts add, so they don't count.
    /// </summary>
    public static bool FileProves(string fileContent, string token) =>
        !string.IsNullOrWhiteSpace(token)
        && fileContent
            .Split('\n')
            .Any(line => string.Equals(line.Trim().Trim('\uFEFF').Trim(), token, StringComparison.Ordinal));
}
