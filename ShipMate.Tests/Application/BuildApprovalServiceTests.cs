using NSubstitute;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class BuildApprovalServiceTests
{
    private const string Head = "1111111111111111111111111111111111111111";
    private const string Earlier = "3333333333333333333333333333333333333333";
    private const string FeatureHead = "4444444444444444444444444444444444444444";

    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly BuildApprovalService _service;

    private readonly Workspace _workspace = new()
    {
        GitHubRepoOwner = "clinic", GitHubRepoName = "booking", GitHubInstallationId = 4242
    };
    private readonly SnapshotFeature _feature = new()
    {
        FeatureId = Guid.NewGuid(), Name = "Booking", Status = SnapshotFeatureStatus.Locked
    };
    private readonly Guid _me = Guid.NewGuid();
    private readonly FeatureAssignment _assignment;
    private readonly BuildSubmission _submission;
    private readonly string _staging;

    public BuildApprovalServiceTests()
    {
        _service = new BuildApprovalService(_guard, _definitions, _assignments, _submissions, _alerts, _app, _notifier);

        var snapshot = new MvpSnapshot { WorkspaceId = _workspace.Id, Version = 3 };
        snapshot.Features.Add(_feature);
        _definitions.GetLatestSnapshotAsync(_workspace.Id).Returns(snapshot);
        _guard.GetWorkspaceAsMemberAsync(_me, _workspace.Id).Returns(_workspace);

        _assignment = new FeatureAssignment
        {
            WorkspaceId = _workspace.Id, FeatureId = _feature.FeatureId, AssignedUserId = _me,
            BranchName = $"feature/{_feature.FeatureId}-me-dev"
        };
        _assignments.GetByWorkspaceIdAsync(_workspace.Id).Returns(new List<FeatureAssignment> { _assignment });

        _staging = $"staging/{_feature.FeatureId}-me-dev";
        _submission = new BuildSubmission
        {
            WorkspaceId = _workspace.Id, FeatureId = _feature.FeatureId, StagingBranch = _staging, HeadSha = Head,
            Status = BuildSubmissionStatus.InScope, ConfidenceScore = 0.9m, ThresholdAtCheck = 0.75m
        };
        _submissions.GetByIdAsync(_submission.Id).Returns(_submission);
        _submissions.GetLatestAsync(_workspace.Id, _feature.FeatureId).Returns(_submission);

        _alerts.GetOpenByFeatureAsync(_workspace.Id, _feature.FeatureId).Returns(new List<DriftAlert>());
        _app.FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Arg.Any<string>()).Returns(true);
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging).Returns(Head);
    }

    private Task<BuildSubmissionDto> Approve(string? commitSha = null, Guid? by = null) =>
        _service.ApproveAsync(by ?? _me, _workspace.Id, _submission.Id, new ApproveBuildRequest { CommitSha = commitSha });

    private async Task AssertRefused<T>(Func<Task> act) where T : Exception
    {
        await Assert.ThrowsAsync<T>(act);
        await _app.DidNotReceiveWithAnyArgs().FastForwardBranchAsync(default, default!, default!, default!, default!);
        Assert.NotEqual(BuildSubmissionStatus.Approved, _submission.Status);
    }

    // ---- the approval ----

    [Fact]
    public async Task AnInScopeSubmission_MovesTheFeatureBranchToItsHeadAndIsApproved()
    {
        var result = await Approve();

        await _app.Received(1).FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Head);
        Assert.Equal(BuildSubmissionStatus.Approved, _submission.Status);
        Assert.Equal(Head, _submission.ApprovedSha);
        Assert.Equal(_me, _submission.ApprovedByUserId);
        Assert.NotNull(_submission.ApprovedAt);
        Assert.Equal(Head, _assignment.LastApprovedSha);
        Assert.Equal(BuildSubmissionStatus.Approved, result.Status);
        Assert.Equal(Head, result.ApprovedSha);
        await _submissions.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task TheStagingBranch_IsDeletedWhenItHasNothingNewerThanTheApprovedCommit()
    {
        await Approve();

        await _app.Received(1).DeleteBranchAsync(4242, "clinic", "booking", _staging);
    }

    [Fact]
    public async Task TheStagingBranch_IsKeptWhenTheDeveloperPushedMoreSince()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging).Returns("9999999999999999999999999999999999999999");

        await Approve();

        await _app.DidNotReceiveWithAnyArgs().DeleteBranchAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task AStagingCleanupThatFails_DoesNotUndoTheApproval()
    {
        _app.DeleteBranchAsync(4242, "clinic", "booking", _staging)
            .Returns(Task.FromException(new GitHubRequestFailedException()));

        var result = await Approve();

        Assert.Equal(BuildSubmissionStatus.Approved, result.Status);
    }

    [Fact]
    public async Task AMaintenanceOnlySubmission_CarryingAnInScopeVerdict_CanBeApproved()
    {
        _submission.Status = BuildSubmissionStatus.SkippedMaintenance;

        await Approve();

        Assert.Equal(BuildSubmissionStatus.Approved, _submission.Status);
    }

    // ---- real-time messages ----

    [Fact]
    public async Task Approving_AnnouncesTheApprovalAndThePushToTheWorkspace()
    {
        await Approve();

        await _notifier.Received(1).PublishAsync(_workspace.Id, WorkspaceEvents.CodeScopeApproved, Arg.Any<object>());
        await _notifier.Received(1).PublishAsync(_workspace.Id, WorkspaceEvents.BranchPushSuccess, Arg.Any<object>());
    }

    [Fact]
    public async Task AFastForwardGitHubRefused_AnnouncesNothing()
    {
        _app.FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Head).Returns(false);

        await Assert.ThrowsAsync<FastForwardFailedException>(() => Approve());

        await _notifier.DidNotReceiveWithAnyArgs().PublishAsync(default, default!, default!);
    }

    // ---- the check run ----

    [Fact]
    public async Task Approving_PutsTheBotsScopeCheckOnTheApprovedCommit()
    {
        await Approve();

        await _app.Received(1).CreateScopeCheckRunAsync(
            4242, "clinic", "booking", Head, "Scope check passed", Arg.Is<string>(s => s.Contains("Booking") && s.Contains("0.90")));
    }

    [Fact]
    public async Task ApprovingAnEarlierCommit_PutsTheCheckOnThatCommit()
    {
        GivenFeatureBranchAt(FeatureHead, Earlier, Head);
        _app.FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Earlier).Returns(true);

        await Approve(Earlier);

        await _app.Received(1).CreateScopeCheckRunAsync(4242, "clinic", "booking", Earlier, Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ADriftTheManagerOverrode_SaysSoInTheCheck()
    {
        _submission.Status = BuildSubmissionStatus.Drift;
        _submission.ConfidenceScore = 0.4m;
        _alerts.GetLatestByFeatureAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new DriftAlert { Status = DriftAlertStatus.Overridden });

        await Approve();

        await _app.Received(1).CreateScopeCheckRunAsync(
            4242, "clinic", "booking", Head, Arg.Any<string>(), Arg.Is<string>(s => s.Contains("overrode")));
    }

    [Fact]
    public async Task WhenTheCheckCannotBeCreated_TheBranchStaysAndTheApprovalCanBeRepeated()
    {
        _app.CreateScopeCheckRunAsync(default, default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(Task.FromException(new GitHubRequestFailedException()));

        await Assert.ThrowsAsync<GitHubRequestFailedException>(() => Approve());

        await _app.DidNotReceiveWithAnyArgs().FastForwardBranchAsync(default, default!, default!, default!, default!);
        Assert.Equal(BuildSubmissionStatus.InScope, _submission.Status);
        await _submissions.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task ARefusedApproval_PutsNoCheckAnywhere()
    {
        _submission.Status = BuildSubmissionStatus.Drift;
        _submission.ConfidenceScore = 0.4m;

        await Assert.ThrowsAsync<DriftUnresolvedException>(() => Approve());

        await _app.DidNotReceiveWithAnyArgs().CreateScopeCheckRunAsync(default, default!, default!, default!, default!, default!);
    }

    // ---- the verdict decides ----

    [Fact]
    public async Task ADriftWhoseAlertIsOverridden_CanBeApproved()
    {
        _submission.Status = BuildSubmissionStatus.Drift;
        _submission.ConfidenceScore = 0.4m;
        _alerts.GetLatestByFeatureAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new DriftAlert { Status = DriftAlertStatus.Overridden });

        await Approve();

        Assert.Equal(BuildSubmissionStatus.Approved, _submission.Status);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Unresolved)]
    [InlineData(DriftAlertStatus.Acknowledged)]
    public async Task AnOpenAlert_BlocksTheApproval_AcknowledgingDoesNotUnlockIt(DriftAlertStatus status)
    {
        _submission.Status = BuildSubmissionStatus.Drift;
        _submission.ConfidenceScore = 0.4m;
        var open = new DriftAlert { Status = status };
        _alerts.GetOpenByFeatureAsync(_workspace.Id, _feature.FeatureId).Returns(new List<DriftAlert> { open });
        _alerts.GetLatestByFeatureAsync(_workspace.Id, _feature.FeatureId).Returns(open);

        await AssertRefused<DriftUnresolvedException>(() => Approve());
    }

    [Fact]
    public async Task AnInScopeSubmission_WithAnOpenAlertLeftOver_IsBlocked()
    {
        _alerts.GetOpenByFeatureAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new List<DriftAlert> { new() { Status = DriftAlertStatus.Unresolved } });

        await AssertRefused<DriftUnresolvedException>(() => Approve());
    }

    [Fact]
    public async Task ADriftWithoutAnOverride_IsBlocked()
    {
        _submission.Status = BuildSubmissionStatus.Drift;
        _submission.ConfidenceScore = 0.4m;
        _alerts.GetLatestByFeatureAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new DriftAlert { Status = DriftAlertStatus.Resolved });

        await AssertRefused<DriftUnresolvedException>(() => Approve());
    }

    [Fact]
    public async Task AMaintenanceOnlySubmission_CarryingADriftVerdict_NeedsTheOverrideToo()
    {
        _submission.Status = BuildSubmissionStatus.SkippedMaintenance;
        _submission.ConfidenceScore = 0.4m;
        _alerts.GetLatestByFeatureAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new DriftAlert { Status = DriftAlertStatus.Acknowledged });
        _alerts.GetOpenByFeatureAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new List<DriftAlert> { new() { Status = DriftAlertStatus.Acknowledged } });

        await AssertRefused<DriftUnresolvedException>(() => Approve());
    }

    // ---- the submission must be ready and the newest ----

    [Theory]
    [InlineData(BuildSubmissionStatus.Analyzing)]
    [InlineData(BuildSubmissionStatus.Failed)]
    [InlineData(BuildSubmissionStatus.Approved)]
    public async Task ASubmissionThatIsNotAwaitingApproval_IsRefused(BuildSubmissionStatus status)
    {
        _submission.Status = status;

        await Assert.ThrowsAsync<SubmissionNotReadyException>(() => Approve());
        await _app.DidNotReceiveWithAnyArgs().FastForwardBranchAsync(default, default!, default!, default!, default!);
    }

    [Fact]
    public async Task ASupersededSubmission_IsRefused()
    {
        _submission.Status = BuildSubmissionStatus.Superseded;

        await AssertRefused<SubmissionSupersededException>(() => Approve());
    }

    [Fact]
    public async Task ANewerSubmissionOfTheFeature_MakesThisOneStale()
    {
        _submissions.GetLatestAsync(_workspace.Id, _feature.FeatureId)
            .Returns(new BuildSubmission { WorkspaceId = _workspace.Id, FeatureId = _feature.FeatureId });

        await AssertRefused<SubmissionSupersededException>(() => Approve());
    }

    [Fact]
    public async Task ASubmissionOfAnotherWorkspace_IsNotFound()
    {
        _submission.WorkspaceId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => Approve());
    }

    // ---- who may approve ----

    [Fact]
    public async Task OnlyTheDeveloperHoldingTheFeatureMayApprove()
    {
        var other = Guid.NewGuid();
        _guard.GetWorkspaceAsMemberAsync(other, _workspace.Id).Returns(_workspace);

        await AssertRefused<NotFeatureHolderException>(() => Approve(by: other));
    }

    [Fact]
    public async Task AFeatureNobodyHoldsAnymore_CannotBeApproved()
    {
        _assignment.ReleasedAt = DateTime.UtcNow;

        await AssertRefused<NotFeatureHolderException>(() => Approve());
    }

    [Fact]
    public async Task AFeatureRemovedFromTheScope_CannotBeApproved()
    {
        _feature.Status = SnapshotFeatureStatus.Replaced;

        await AssertRefused<FeatureReplacedException>(() => Approve());
    }

    [Fact]
    public async Task AWorkspaceThatLostItsApp_CannotApprove()
    {
        _workspace.GovernanceSuspendedAt = DateTime.UtcNow;

        await AssertRefused<GovernanceSuspendedException>(() => Approve());
    }

    [Fact]
    public async Task AWorkspaceWithoutALinkedRepo_CannotApprove()
    {
        _workspace.GitHubInstallationId = null;

        await AssertRefused<WorkspaceRepoNotLinkedException>(() => Approve());
    }

    // ---- choosing an earlier commit ----

    private void GivenFeatureBranchAt(string sha, params string[] pendingCommits)
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _assignment.BranchName).Returns(sha);
        _app.CompareAsync(4242, "clinic", "booking", sha, Head)
            .Returns(new GitHubComparison(["c"], [], false, pendingCommits));
    }

    [Fact]
    public async Task AnEarlierCommitOfTheSubmission_IsWhatTheBranchMovesTo()
    {
        GivenFeatureBranchAt(FeatureHead, Earlier, Head);
        _app.FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Earlier).Returns(true);

        var result = await Approve(Earlier);

        await _app.Received(1).FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Earlier);
        Assert.Equal(Earlier, result.ApprovedSha);
        Assert.Equal(Earlier, _assignment.LastApprovedSha);
    }

    [Fact]
    public async Task AnEarlierCommit_LeavesTheStagingBranchBecauseItHoldsMore()
    {
        GivenFeatureBranchAt(FeatureHead, Earlier, Head);
        _app.FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Earlier).Returns(true);

        await Approve(Earlier);

        await _app.DidNotReceiveWithAnyArgs().DeleteBranchAsync(default, default!, default!, default!);
    }

    [Fact]
    public async Task ACommitThatIsNotAmongThePendingOnes_IsRefused()
    {
        GivenFeatureBranchAt(FeatureHead, Head);

        await AssertRefused<CommitNotInSubmissionException>(() => Approve(Earlier));
    }

    [Fact]
    public async Task ACommitWhenGitHubCannotCompare_IsRefused()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _assignment.BranchName).Returns(FeatureHead);
        _app.CompareAsync(4242, "clinic", "booking", FeatureHead, Head).Returns((GitHubComparison?)null);

        await AssertRefused<CommitNotInSubmissionException>(() => Approve(Earlier));
    }

    [Fact]
    public async Task AskingForTheHeadByName_NeedsNoComparison()
    {
        await Approve(Head.ToUpperInvariant());

        await _app.DidNotReceiveWithAnyArgs().CompareAsync(default, default!, default!, default!, default!);
    }

    // ---- GitHub refuses ----

    [Fact]
    public async Task AFeatureBranchThatDivergedFromTheCommit_FailsTheFastForwardAndChangesNothing()
    {
        _app.FastForwardBranchAsync(4242, "clinic", "booking", _assignment.BranchName, Head).Returns(false);

        await Assert.ThrowsAsync<FastForwardFailedException>(() => Approve());

        Assert.Equal(BuildSubmissionStatus.InScope, _submission.Status);
        Assert.Null(_assignment.LastApprovedSha);
        await _submissions.DidNotReceive().SaveChangesAsync();
    }
}
