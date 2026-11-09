using NSubstitute;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

public class DirectPushDetectorTests
{
    private const string Approved = "1111111111111111111111111111111111111111";
    private const string Foreign = "2222222222222222222222222222222222222222";
    private const string MainOld = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string MainNew = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string CommitOne = "c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1";
    private const string CommitTwo = "c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2";

    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
    private readonly InMemoryAuditTrailRepository _auditEntries = new();
    private readonly DirectPushDetector _detector;
    private readonly Workspace _workspace = new()
    {
        GitHubRepoOwner = "clinic", GitHubRepoName = "booking", GitHubInstallationId = 4242
    };
    private readonly FeatureAssignment _assignment = new()
    {
        FeatureId = Guid.NewGuid(), BranchName = "feature/x-me", LastApprovedSha = Approved
    };

    public DirectPushDetectorTests()
    {
        _detector = new DirectPushDetector(_app, new AuditTrail(_auditEntries));

        _workspace.LastAuditedMainSha = MainOld;
        _app.GetDefaultBranchAsync(4242, "clinic", "booking").Returns("main");
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns(Approved);
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "main").Returns(MainOld);
    }

    private Task Detect() => _detector.DetectAsync(_workspace, _assignment);

    private void GivenNewMainCommits(bool truncated = false, params string[] commits)
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "main").Returns(MainNew);
        _app.CompareAsync(4242, "clinic", "booking", MainOld, MainNew)
            .Returns(new GitHubComparison([], [], truncated, commits));
    }

    private void GivenPullRequest(string commit, string headSha, bool passing)
    {
        _app.GetMergedPullRequestsOfCommitAsync(4242, "clinic", "booking", commit)
            .Returns(new List<GitHubPullRequestRef> { new(1, headSha) });
        _app.HasPassingScopeCheckAsync(4242, "clinic", "booking", headSha).Returns(passing);
    }

    // ---- the feature branch ----

    [Fact]
    public async Task AFeatureBranchAtTheLastApprovedCommit_IsFine()
    {
        await Detect();

        Assert.Empty(_auditEntries.Entries);
    }

    [Fact]
    public async Task AFeatureBranchMovedByHand_IsRecordedOnce()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns(Foreign);

        await Detect();
        await Detect();

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Equal(AuditTypes.DirectPushDetected, record.AuditType);
        Assert.Null(record.ActorUserId);
        Assert.Contains(Foreign, record.Payload);
        Assert.Contains(Approved, record.Payload);
        Assert.Equal(Foreign, _assignment.FlaggedHeadSha);
    }

    [Fact]
    public async Task AFurtherPushToTheSameBranch_IsRecordedAgain()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns(Foreign);
        await Detect();
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me")
            .Returns("3333333333333333333333333333333333333333");

        await Detect();

        Assert.Equal(2, _auditEntries.Entries.Count);
    }

    [Fact]
    public async Task ABranchNothingWasApprovedOnYet_WithCommitsBeyondMain_IsRecorded()
    {
        _assignment.LastApprovedSha = null;
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns(Foreign);
        _app.CompareAsync(4242, "clinic", "booking", "main", Foreign)
            .Returns(new GitHubComparison([], [], false, [Foreign]));

        await Detect();

        Assert.Single(_auditEntries.Entries);
    }

    [Fact]
    public async Task ABranchNothingWasApprovedOnYet_StillWithinMain_IsFine()
    {
        _assignment.LastApprovedSha = null;
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns(MainOld);
        _app.CompareAsync(4242, "clinic", "booking", "main", MainOld)
            .Returns(new GitHubComparison([], [], false, []));

        await Detect();

        Assert.Empty(_auditEntries.Entries);
    }

    [Fact]
    public async Task AFeatureBranchThatIsGone_IsNotAFinding()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns((string?)null);

        await Detect();

        Assert.Empty(_auditEntries.Entries);
    }

    // ---- the default branch ----

    [Fact]
    public async Task TheFirstLook_OnlyMarksWhereCheckingStarts()
    {
        _workspace.LastAuditedMainSha = null;

        await Detect();

        Assert.Equal(MainOld, _workspace.LastAuditedMainSha);
        Assert.Empty(_auditEntries.Entries);
        await _app.DidNotReceiveWithAnyArgs().CompareAsync(default, default!, default!, default!, default!);
    }

    [Fact]
    public async Task NothingNewOnMain_DoesNothing()
    {
        await Detect();

        Assert.Equal(MainOld, _workspace.LastAuditedMainSha);
        await _app.DidNotReceiveWithAnyArgs().CompareAsync(default, default!, default!, default!, default!);
    }

    [Fact]
    public async Task ACommitThatCameThroughACheckedPullRequest_IsFine()
    {
        GivenNewMainCommits(commits: CommitOne);
        GivenPullRequest(CommitOne, "pr-head", passing: true);

        await Detect();

        Assert.Empty(_auditEntries.Entries);
        Assert.Equal(MainNew, _workspace.LastAuditedMainSha);
    }

    [Fact]
    public async Task ACommitWithNoPullRequest_IsRecorded()
    {
        GivenNewMainCommits(commits: CommitOne);
        _app.GetMergedPullRequestsOfCommitAsync(4242, "clinic", "booking", CommitOne)
            .Returns(new List<GitHubPullRequestRef>());

        await Detect();

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Contains("NO_PULL_REQUEST", record.Payload);
        Assert.Contains(CommitOne, record.Payload);
        Assert.Equal(MainNew, _workspace.LastAuditedMainSha);
    }

    [Fact]
    public async Task ACommitFromAPullRequestWithoutTheShipMateCheck_IsRecorded()
    {
        GivenNewMainCommits(commits: CommitOne);
        GivenPullRequest(CommitOne, "pr-head", passing: false);

        await Detect();

        Assert.Contains("NO_SCOPE_CHECK", Assert.Single(_auditEntries.Entries).Payload);
    }

    [Fact]
    public async Task SeveralCommitsOfOnePullRequest_AskGitHubAboutItsCheckOnlyOnce()
    {
        GivenNewMainCommits(commits: [CommitOne, CommitTwo]);
        GivenPullRequest(CommitOne, "pr-head", passing: true);
        GivenPullRequest(CommitTwo, "pr-head", passing: true);

        await Detect();

        await _app.Received(1).HasPassingScopeCheckAsync(4242, "clinic", "booking", "pr-head");
    }

    [Fact]
    public async Task OnlyTheOffendingCommitsAreRecorded()
    {
        GivenNewMainCommits(commits: [CommitOne, CommitTwo]);
        GivenPullRequest(CommitOne, "pr-head", passing: true);
        _app.GetMergedPullRequestsOfCommitAsync(4242, "clinic", "booking", CommitTwo)
            .Returns(new List<GitHubPullRequestRef>());

        await Detect();

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Contains(CommitTwo, record.Payload);
    }

    [Fact]
    public async Task AMainWhoseHistoryWasRewritten_IsRecordedAndTheBookmarkMovesOn()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "main").Returns(MainNew);
        _app.CompareAsync(4242, "clinic", "booking", MainOld, MainNew).Returns((GitHubComparison?)null);

        await Detect();

        Assert.Contains("HISTORY_REWRITTEN", Assert.Single(_auditEntries.Entries).Payload);
        Assert.Equal(MainNew, _workspace.LastAuditedMainSha);
    }

    [Fact]
    public async Task MoreCommitsThanGitHubLists_ContinueFromTheLastOneExamined()
    {
        GivenNewMainCommits(truncated: true, commits: [CommitOne, CommitTwo]);
        GivenPullRequest(CommitOne, "pr-head", passing: true);
        GivenPullRequest(CommitTwo, "pr-head", passing: true);

        await Detect();

        Assert.Equal(CommitTwo, _workspace.LastAuditedMainSha);
    }

    // ---- never in the way ----

    [Fact]
    public async Task WhenGitHubFailsHalfWay_NothingIsRecordedAndTheBookmarkStays()
    {
        GivenNewMainCommits(commits: [CommitOne, CommitTwo]);
        GivenPullRequest(CommitOne, "pr-head", passing: false);
        _app.GetMergedPullRequestsOfCommitAsync(4242, "clinic", "booking", CommitTwo)
            .Returns(Task.FromException<IReadOnlyList<GitHubPullRequestRef>>(new GitHubRequestFailedException()));

        await Detect();

        Assert.Empty(_auditEntries.Entries);
        Assert.Equal(MainOld, _workspace.LastAuditedMainSha);
    }

    [Fact]
    public async Task WhenGitHubIsDown_TheDetectionEndsQuietly()
    {
        _app.GetDefaultBranchAsync(4242, "clinic", "booking")
            .Returns(Task.FromException<string>(new GitHubRequestFailedException()));

        var error = await Record.ExceptionAsync(Detect);

        Assert.Null(error);
    }

    [Fact]
    public async Task TheRecordsFormAnIntactChain()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", "feature/x-me").Returns(Foreign);
        GivenNewMainCommits(commits: [CommitOne, CommitTwo]);
        _app.GetMergedPullRequestsOfCommitAsync(4242, "clinic", "booking", Arg.Any<string>())
            .Returns(new List<GitHubPullRequestRef>());

        await Detect();

        Assert.Equal(3, _auditEntries.Entries.Count);
        Assert.Null(AuditChain.FindFirstBroken(_auditEntries.Entries));
    }
}
