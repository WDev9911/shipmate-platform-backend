using ShipMate.Domain.ValueObjects;

namespace ShipMate.Application.Interfaces.Services;

public record GitHubAppInstallation(long Id, bool IsOrganization);

public record GitHubPullRequestRef(int Number, string HeadSha);

/// <summary>The status check that lets code into the default branch; only the Bot creates it.</summary>
public static class GitHubScopeCheck
{
    public const string Name = "ShipMate Scope Check";
}

/// <summary>What changed between two points of a repository: the commits (messages and hashes) and the changed files.</summary>
public record GitHubComparison(
    IReadOnlyList<string> CommitMessages, IReadOnlyList<ChangedFile> Files, bool Truncated,
    IReadOnlyList<string>? CommitShas = null);

/// <summary>What the ShipMate Bot (the GitHub App) does on a linked repository.</summary>
public interface IGitHubAppService
{
    /// <summary>Where a repository owner installs the App; shown to the Manager when it is missing.</summary>
    string InstallUrl { get; }

    /// <summary>The App's installation on the repository, or null when the App isn't installed there.</summary>
    Task<GitHubAppInstallation?> FindInstallationAsync(string owner, string repo);

    /// <summary>
    /// The commit a branch currently points to, or null when the branch doesn't exist. Throws
    /// GitHubAppUninstalledException when the App was removed from the repo, GitHubRequestFailedException on other failures.
    /// </summary>
    Task<string?> GetBranchHeadShaAsync(long installationId, string owner, string repo, string branchName);

    Task<string> GetDefaultBranchAsync(long installationId, string owner, string repo);

    /// <summary>
    /// Everything that changed from <paramref name="baseRef"/> (a branch or a commit) to <paramref name="headSha"/>.
    /// Null when GitHub doesn't know one of them, for instance a commit that a force push removed. Truncated is set
    /// when the change is too big for GitHub to list completely.
    /// </summary>
    Task<GitHubComparison?> CompareAsync(long installationId, string owner, string repo, string baseRef, string headSha);

    /// <summary>
    /// Creates the branch from the head of the default branch and protects it so only the Bot can write to it.
    /// Safe to repeat; throws GitHubBranchCreationFailedException when GitHub refuses.
    /// </summary>
    Task CreateProtectedBranchAsync(
        long installationId, bool isOrganization, string owner, string repo, string branchName);

    /// <summary>
    /// Moves the branch forward to <paramref name="sha"/> without forcing. False when GitHub refuses because the branch
    /// is not an ancestor of the commit (or the commit is gone); throws GitHubRequestFailedException on other failures.
    /// </summary>
    Task<bool> FastForwardBranchAsync(long installationId, string owner, string repo, string branchName, string sha);

    /// <summary>Deletes the branch. A branch that is already gone is fine.</summary>
    Task DeleteBranchAsync(long installationId, string owner, string repo, string branchName);

    /// <summary>
    /// Makes merging into the default branch require the "ShipMate Scope Check" from this App, keeping every other
    /// rule the branch already has. False when it could not be switched on: a personal repo whose plan has no branch
    /// protection, or a branch that is protected without status checks (which would have to be rewritten, not extended).
    /// Throws GitHubRequestFailedException when an organization repo refuses.
    /// </summary>
    Task<bool> EnsureMergeGateAsync(long installationId, bool isOrganization, string owner, string repo);

    /// <summary>Marks the commit as checked: a successful "ShipMate Scope Check" from the Bot.</summary>
    Task CreateScopeCheckRunAsync(
        long installationId, string owner, string repo, string headSha, string title, string summary);

    /// <summary>Whether the commit carries a successful "ShipMate Scope Check" made by this App.</summary>
    Task<bool> HasPassingScopeCheckAsync(long installationId, string owner, string repo, string headSha);

    /// <summary>The merged pull requests a commit of the default branch came in through.</summary>
    Task<IReadOnlyList<GitHubPullRequestRef>> GetMergedPullRequestsOfCommitAsync(
        long installationId, string owner, string repo, string commitSha);
}
