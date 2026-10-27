using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;

namespace ShipMate.Application.Services;

public class DirectPushDetector : IDirectPushDetector
{
    private readonly IGitHubAppService _gitHubAppService;
    private readonly IAuditTrail _auditTrail;

    public DirectPushDetector(IGitHubAppService gitHubAppService, IAuditTrail auditTrail)
    {
        _gitHubAppService = gitHubAppService;
        _auditTrail = auditTrail;
    }

    public async Task DetectAsync(Workspace workspace, FeatureAssignment assignment)
    {
        var repo = new Repo(workspace.GitHubInstallationId!.Value, workspace.GitHubRepoOwner!, workspace.GitHubRepoName!);

        try
        {
            var defaultBranch = await _gitHubAppService.GetDefaultBranchAsync(repo.InstallationId, repo.Owner, repo.Name);
            await CheckFeatureBranchAsync(workspace, assignment, repo, defaultBranch);
            await CheckDefaultBranchAsync(workspace, repo, defaultBranch);
        }
        catch (AppException)
        {
            // GitHub didn't answer or the App is gone (submit handles that): look again at the next submit.
        }
    }

    // Only the Bot moves a feature branch, so a head other than the last approved commit was pushed around it.
    private async Task CheckFeatureBranchAsync(Workspace workspace, FeatureAssignment assignment, Repo repo, string defaultBranch)
    {
        var head = await _gitHubAppService.GetBranchHeadShaAsync(repo.InstallationId, repo.Owner, repo.Name, assignment.BranchName);
        if (head is null || Same(head, assignment.LastApprovedSha) || Same(head, assignment.FlaggedHeadSha))
        {
            return;
        }

        // Nothing approved yet: the branch should still sit on the commit of the default branch it was cut from,
        // which is somewhere in main's history. Any commit beyond main's history was put there by hand.
        if (assignment.LastApprovedSha is null)
        {
            var beyondMain = await _gitHubAppService.CompareAsync(
                repo.InstallationId, repo.Owner, repo.Name, defaultBranch, head);
            if (beyondMain?.CommitShas is not { Count: > 0 })
            {
                return;
            }
        }

        await _auditTrail.AppendAsync(workspace.Id, AuditTypes.DirectPushDetected, null, new
        {
            branch = assignment.BranchName,
            featureId = assignment.FeatureId,
            headSha = head,
            expectedSha = assignment.LastApprovedSha
        });
        assignment.FlaggedHeadSha = head;
    }

    // Everything new on the default branch must have come in through a merged pull request carrying the ShipMate check.
    private async Task CheckDefaultBranchAsync(Workspace workspace, Repo repo, string defaultBranch)
    {
        var head = await _gitHubAppService.GetBranchHeadShaAsync(repo.InstallationId, repo.Owner, repo.Name, defaultBranch);
        if (head is null || Same(head, workspace.LastAuditedMainSha))
        {
            return;
        }

        // The first look only marks where checking starts: what came before ShipMate can't be judged.
        if (workspace.LastAuditedMainSha is null)
        {
            workspace.LastAuditedMainSha = head;
            return;
        }

        var since = await _gitHubAppService.CompareAsync(
            repo.InstallationId, repo.Owner, repo.Name, workspace.LastAuditedMainSha, head);
        if (since is null)
        {
            // The commit we stopped at is gone: the history was rewritten.
            await _auditTrail.AppendAsync(workspace.Id, AuditTypes.DirectPushDetected, null, new
            {
                branch = defaultBranch,
                reason = "HISTORY_REWRITTEN",
                fromSha = workspace.LastAuditedMainSha,
                headSha = head
            });
            workspace.LastAuditedMainSha = head;
            return;
        }

        var commits = since.CommitShas ?? [];
        var checkedPullRequests = new Dictionary<string, bool>();
        var findings = new List<(string Commit, string Reason)>();
        foreach (var commit in commits)
        {
            var reason = await WhyNotReviewedAsync(repo, commit, checkedPullRequests);
            if (reason is not null)
            {
                findings.Add((commit, reason));
            }
        }

        // Written only once every commit has been looked at, so a failure half way leaves nothing to report twice.
        foreach (var (commit, reason) in findings)
        {
            await _auditTrail.AppendAsync(workspace.Id, AuditTypes.DirectPushDetected, null, new
            {
                branch = defaultBranch,
                commitSha = commit,
                reason
            });
        }

        // Past what GitHub would list, the next look carries on from the last commit that was examined.
        workspace.LastAuditedMainSha = since.Truncated && commits.Count > 0 ? commits[^1] : head;
    }

    private async Task<string?> WhyNotReviewedAsync(Repo repo, string commit, Dictionary<string, bool> passingByPullRequestHead)
    {
        var pulls = await _gitHubAppService.GetMergedPullRequestsOfCommitAsync(
            repo.InstallationId, repo.Owner, repo.Name, commit);
        if (pulls.Count == 0)
        {
            return "NO_PULL_REQUEST";
        }

        foreach (var pull in pulls)
        {
            if (!passingByPullRequestHead.TryGetValue(pull.HeadSha, out var passing))
            {
                passing = await _gitHubAppService.HasPassingScopeCheckAsync(
                    repo.InstallationId, repo.Owner, repo.Name, pull.HeadSha);
                passingByPullRequestHead[pull.HeadSha] = passing;
            }

            if (passing)
            {
                return null;
            }
        }

        return "NO_SCOPE_CHECK";
    }

    private static bool Same(string? left, string? right) =>
        right is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record Repo(long InstallationId, string Owner, string Name);
}
