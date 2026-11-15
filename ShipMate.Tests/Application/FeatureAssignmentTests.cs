using NSubstitute;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Tests.Application;

public class FeatureAssignmentTests
{
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IGitHubAppService _gitHub = Substitute.For<IGitHubAppService>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly Workspace _workspace = new();
    private readonly FeatureAssignmentService _service;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly WorkspaceMember _me;
    private readonly SnapshotFeature _easy = SnapshotFeatureOf("easy", complexity: 2);
    private readonly SnapshotFeature _hard = SnapshotFeatureOf("hard", complexity: 5);

    public FeatureAssignmentTests()
    {
        _service = new FeatureAssignmentService(
            _definitions, _assignments, _members, _guard, _gitHub, _notifier, _submissions, _alerts);
        _guard.GetWorkspaceAsMemberAsync(Arg.Any<Guid>(), _workspaceId).Returns(_workspace);
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([]);
        _submissions.GetLatestApprovalPerFeatureAsync(_workspaceId).Returns([]);
        _alerts.GetByWorkspaceIdAsync(_workspaceId, null).Returns([]);

        _me = Member("Me", skill: 3, github: "me-dev");
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns([_me]);
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns([]);
        _assignments.TryAddAsync(Arg.Any<FeatureAssignment>()).Returns(true);

        var snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 1 };
        snapshot.Features.Add(_easy);
        snapshot.Features.Add(_hard);
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(snapshot);
    }

    private static SnapshotFeature SnapshotFeatureOf(string name, int complexity) =>
        new() { FeatureId = Guid.NewGuid(), Name = name, Complexity = complexity, Status = SnapshotFeatureStatus.Locked };

    private static WorkspaceMember Member(string name, int? skill, string? github) => new()
    {
        UserId = Guid.NewGuid(),
        Status = WorkspaceMemberStatus.Active,
        SkillLevel = skill,
        User = new User { DisplayName = name, GitHubUsername = github }
    };

    private Task<ShipMate.Application.DTOs.ProductDefinitions.FeatureAssignmentDto> Claim(SnapshotFeature feature) =>
        _service.ClaimAsync(_me.UserId, _workspaceId, feature.FeatureId);

    // ---- policy ----

    [Theory]
    [InlineData(3, 3, true)]
    [InlineData(4, 3, true)]
    [InlineData(2, 3, false)]
    [InlineData(null, 1, false)]
    public void IsQualified_NeedsSkillAtLeastTheComplexity(int? skill, int complexity, bool expected) =>
        Assert.Equal(expected, FeatureAssignmentPolicy.IsQualified(skill, complexity));

    [Fact]
    public void BranchName_FollowsTheConvention()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        Assert.Equal("feature/11111111-1111-1111-1111-111111111111-octocat", FeatureAssignmentPolicy.BranchNameFor(id, "octocat"));
    }

    // ---- claim ----

    [Fact]
    public async Task Claim_QualifiedDeveloper_GetsTheFeatureAndTheBranchName()
    {
        var result = await Claim(_easy);

        Assert.Equal(_me.UserId, result.AssignedUserId);
        Assert.Equal($"feature/{_easy.FeatureId}-me-dev", result.BranchName);
        await _assignments.Received(1).TryAddAsync(Arg.Is<FeatureAssignment>(a =>
            a.WorkspaceId == _workspaceId && a.FeatureId == _easy.FeatureId && a.AssignedUserId == _me.UserId));
    }

    [Fact]
    public async Task Claim_SkillBelowComplexity_IsRejectedAndFeatureStaysFree()
    {
        await Assert.ThrowsAsync<SkillTooLowForFeatureException>(() => Claim(_hard));

        await _assignments.DidNotReceive().TryAddAsync(Arg.Any<FeatureAssignment>());
    }

    [Fact]
    public async Task Claim_SkillNotDeclared_IsRejected()
    {
        _me.SkillLevel = null;

        await Assert.ThrowsAsync<SkillNotDeclaredException>(() => Claim(_easy));
    }

    [Fact]
    public async Task Claim_WithoutGitHubUsername_IsRejected()
    {
        _me.User.GitHubUsername = null;

        await Assert.ThrowsAsync<GitHubNotConnectedException>(() => Claim(_easy));
        await _assignments.DidNotReceive().TryAddAsync(Arg.Any<FeatureAssignment>());
    }

    [Fact]
    public async Task Claim_SomeoneElseWasFaster_IsRejected()
    {
        _assignments.TryAddAsync(Arg.Any<FeatureAssignment>()).Returns(false);

        await Assert.ThrowsAsync<FeatureAlreadyAssignedException>(() => Claim(_easy));
    }

    [Fact]
    public async Task Claim_FeatureNotInTheSnapshot_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ClaimAsync(_me.UserId, _workspaceId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Claim_NeverLocked_IsNotFound()
    {
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Claim(_easy));
    }

    [Fact]
    public async Task Claim_InvitedButNotActive_IsRejected()
    {
        _me.Status = WorkspaceMemberStatus.Invited;

        await Assert.ThrowsAsync<NotFoundException>(() => Claim(_easy));
    }

    // ---- creating the branch on GitHub ----

    private void GivenLinkedRepo(bool isOrganization = true)
    {
        _workspace.GitHubRepoOwner = "clinic";
        _workspace.GitHubRepoName = "booking";
        _workspace.GitHubInstallationId = 4242;
        _workspace.GitHubRepoIsOrganization = isOrganization;
    }

    [Fact]
    public async Task Claim_WithALinkedRepo_TheBotCreatesAndProtectsTheBranch()
    {
        GivenLinkedRepo();

        var result = await Claim(_easy);

        Assert.True(result.BranchCreated);
        await _gitHub.Received(1).CreateProtectedBranchAsync(4242, true, "clinic", "booking", result.BranchName);
        await _assignments.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Claim_OnAPersonalRepo_TellsTheBotItCannotRestrictPushes()
    {
        GivenLinkedRepo(isOrganization: false);

        var result = await Claim(_easy);

        await _gitHub.Received(1).CreateProtectedBranchAsync(4242, false, "clinic", "booking", result.BranchName);
    }

    [Fact]
    public async Task Claim_WithoutALinkedRepo_OnlyStoresTheBranchName()
    {
        var result = await Claim(_easy);

        Assert.False(result.BranchCreated);
        await _gitHub.DidNotReceiveWithAnyArgs().CreateProtectedBranchAsync(default, default, default!, default!, default!);
    }

    [Fact]
    public async Task Claim_RepoLinkedBeforeTheAppExisted_OnlyStoresTheBranchName()
    {
        GivenLinkedRepo();
        _workspace.GitHubInstallationId = null;

        var result = await Claim(_easy);

        Assert.False(result.BranchCreated);
        await _gitHub.DidNotReceiveWithAnyArgs().CreateProtectedBranchAsync(default, default, default!, default!, default!);
    }

    [Fact]
    public async Task Claim_GitHubRefuses_TheClaimStillStandsWithoutABranch()
    {
        GivenLinkedRepo();
        _gitHub.CreateProtectedBranchAsync(default, default, default!, default!, default!)
            .ReturnsForAnyArgs<Task>(_ => throw new GitHubBranchCreationFailedException());

        var result = await Claim(_easy);

        Assert.False(result.BranchCreated);
        await _assignments.Received(1).TryAddAsync(Arg.Any<FeatureAssignment>());
    }

    [Fact]
    public async Task Claim_BranchNotCreated_WhenProtectionFails_SoARetryRepeatsBothSteps()
    {
        GivenLinkedRepo();
        _gitHub.CreateProtectedBranchAsync(default, default, default!, default!, default!)
            .ReturnsForAnyArgs<Task>(_ => throw new GitHubBranchCreationFailedException());
        var assignment = new FeatureAssignment
        {
            WorkspaceId = _workspaceId, FeatureId = _easy.FeatureId, AssignedUserId = _me.UserId, BranchName = "feature/x"
        };
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns([assignment]);

        await Assert.ThrowsAsync<GitHubBranchCreationFailedException>(() =>
            _service.RetryBranchAsync(_me.UserId, _workspaceId, _easy.FeatureId));

        Assert.Null(assignment.BranchCreatedAt);
    }

    [Fact]
    public async Task RetryBranch_AfterAFailure_CreatesItNow()
    {
        GivenLinkedRepo();
        var assignment = new FeatureAssignment
        {
            WorkspaceId = _workspaceId, FeatureId = _easy.FeatureId, AssignedUserId = _me.UserId, BranchName = "feature/x"
        };
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns([assignment]);

        var result = await _service.RetryBranchAsync(_me.UserId, _workspaceId, _easy.FeatureId);

        Assert.True(result.BranchCreated);
        Assert.NotNull(assignment.BranchCreatedAt);
    }

    [Fact]
    public async Task RetryBranch_WithoutALinkedRepo_IsRejected()
    {
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(
            [new FeatureAssignment { FeatureId = _easy.FeatureId, AssignedUserId = _me.UserId, BranchName = "feature/x" }]);

        await Assert.ThrowsAsync<WorkspaceRepoNotLinkedException>(() =>
            _service.RetryBranchAsync(_me.UserId, _workspaceId, _easy.FeatureId));
    }

    [Fact]
    public async Task RetryBranch_OnSomeoneElsesFeature_IsNotFound()
    {
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(
            [new FeatureAssignment { FeatureId = _easy.FeatureId, AssignedUserId = Guid.NewGuid(), BranchName = "feature/x" }]);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RetryBranchAsync(_me.UserId, _workspaceId, _easy.FeatureId));
    }

    // ---- backup and replaced features ----

    [Fact]
    public async Task Claim_BackupFeature_BecomesLockedInTheSameVersionAndTheHashFollows()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        var snapshot = await _definitions.GetLatestSnapshotAsync(_workspaceId);
        snapshot!.Features.Add(backup);
        snapshot.Hash = "old";

        await Claim(backup);

        Assert.Equal(SnapshotFeatureStatus.Locked, backup.Status);
        Assert.Equal(1, snapshot.Version);
        Assert.NotEqual("old", snapshot.Hash);
        Assert.Equal(64, snapshot.Hash.Length);
    }

    [Fact]
    public async Task Claim_BackupFeature_TellsTheDevelopersWaitingOnIt()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        var waiting = SnapshotFeatureOf("waiting", complexity: 2);
        var snapshot = (await _definitions.GetLatestSnapshotAsync(_workspaceId))!;
        snapshot.Features.Add(backup);
        snapshot.Features.Add(waiting);
        snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = waiting.FeatureId, DependsOnFeatureId = backup.FeatureId });
        var holder = Guid.NewGuid();
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(new List<FeatureAssignment>
        {
            new() { FeatureId = waiting.FeatureId, AssignedUserId = holder }
        });

        await Claim(backup);

        await _notifier.Received(1).PublishToUsersAsync(
            Arg.Is<IEnumerable<Guid>>(users => users.Single() == holder), WorkspaceEvents.BackupFeatureClaimed, Arg.Any<object>());
    }

    [Fact]
    public async Task Claim_BackupFeature_DoesNotTellTheOneWhoTookIt()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        var waiting = SnapshotFeatureOf("waiting", complexity: 2);
        var snapshot = (await _definitions.GetLatestSnapshotAsync(_workspaceId))!;
        snapshot.Features.Add(backup);
        snapshot.Features.Add(waiting);
        snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = waiting.FeatureId, DependsOnFeatureId = backup.FeatureId });
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(new List<FeatureAssignment>
        {
            new() { FeatureId = waiting.FeatureId, AssignedUserId = _me.UserId }
        });

        await Claim(backup);

        await _notifier.Received(1).PublishToUsersAsync(
            Arg.Is<IEnumerable<Guid>>(users => !users.Any()), WorkspaceEvents.BackupFeatureClaimed, Arg.Any<object>());
    }

    [Fact]
    public async Task Claim_OrdinaryFeature_SendsNoBackupMessage()
    {
        await Claim(_easy);

        await _notifier.DidNotReceiveWithAnyArgs().PublishToUsersAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Claim_BackupFeatureLostToSomeoneElse_StaysBackup()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        (await _definitions.GetLatestSnapshotAsync(_workspaceId))!.Features.Add(backup);
        _assignments.TryAddAsync(Arg.Any<FeatureAssignment>()).Returns(false);

        await Assert.ThrowsAsync<FeatureAlreadyAssignedException>(() => Claim(backup));

        Assert.Equal(SnapshotFeatureStatus.Backup, backup.Status);
    }

    [Fact]
    public async Task Claim_ReplacedFeature_IsNotFound()
    {
        var replaced = SnapshotFeatureOf("replaced", complexity: 1);
        replaced.Status = SnapshotFeatureStatus.Replaced;
        (await _definitions.GetLatestSnapshotAsync(_workspaceId))!.Features.Add(replaced);

        await Assert.ThrowsAsync<NotFoundException>(() => Claim(replaced));
    }

    [Fact]
    public async Task Overview_ListsBackupFeaturesButNotReplacedOnes()
    {
        var backup = SnapshotFeatureOf("backup", complexity: 2);
        backup.Status = SnapshotFeatureStatus.Backup;
        var replaced = SnapshotFeatureOf("replaced", complexity: 1);
        replaced.Status = SnapshotFeatureStatus.Replaced;
        var snapshot = (await _definitions.GetLatestSnapshotAsync(_workspaceId))!;
        snapshot.Features.Add(backup);
        snapshot.Features.Add(replaced);

        var overview = await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        Assert.Contains(overview, o => o.FeatureId == backup.FeatureId && o.Status == SnapshotFeatureStatus.Backup);
        Assert.DoesNotContain(overview, o => o.FeatureId == replaced.FeatureId);
    }

    // ---- overview ----

    [Fact]
    public async Task Overview_ListsOnlyQualifiedMembersPerFeature()
    {
        var expert = Member("Expert", skill: 5, github: "expert");
        _members.GetByWorkspaceIdAsync(_workspaceId).Returns([_me, expert]);

        var overview = await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        Assert.Equal(2, overview.Single(o => o.FeatureId == _easy.FeatureId).QualifiedMembers.Count);
        var hard = overview.Single(o => o.FeatureId == _hard.FeatureId);
        Assert.Equal(expert.UserId, Assert.Single(hard.QualifiedMembers).UserId);
    }

    [Fact]
    public async Task Overview_ShowsWhoTookAFeature()
    {
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(
        [
            new FeatureAssignment
            {
                FeatureId = _easy.FeatureId,
                AssignedUserId = _me.UserId,
                AssignedUser = _me.User,
                BranchName = "feature/x"
            }
        ]);

        var overview = await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        Assert.Equal("Me", overview.Single(o => o.FeatureId == _easy.FeatureId).Assignee!.DisplayName);
        Assert.Null(overview.Single(o => o.FeatureId == _hard.FeatureId).Assignee);
    }

    // ---- overview: BUILD progress ----

    private static readonly DateTime At8 = new(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);

    private BuildSubmission Submission(SnapshotFeature feature, BuildSubmissionStatus status, DateTime createdAt) => new()
    {
        WorkspaceId = _workspaceId,
        FeatureId = feature.FeatureId,
        Status = status,
        ConfidenceScore = 0.90m,
        ThresholdAtCheck = 0.75m,
        CreatedAt = createdAt
    };

    private async Task<FeatureBuildProgressDto> BuildOf(SnapshotFeature feature) =>
        (await _service.GetOverviewAsync(_me.UserId, _workspaceId)).Single(o => o.FeatureId == feature.FeatureId).Build;

    [Fact]
    public async Task Overview_FeatureWithoutSubmissions_IsNotSubmitted()
    {
        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.NotSubmitted, build.State);
        Assert.Null(build.LatestSubmissionId);
        Assert.Null(build.LastApproval);
        Assert.Null(build.OpenAlertId);
    }

    [Fact]
    public async Task Overview_ApprovedFeature_ShowsWhoApprovedAndWhen()
    {
        var approved = Submission(_easy, BuildSubmissionStatus.Approved, At8);
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([approved]);
        _submissions.GetLatestApprovalPerFeatureAsync(_workspaceId).Returns(
            [new ApprovedSubmissionInfo(_easy.FeatureId, approved.Id, At8.AddMinutes(5), _me.UserId, "Me", "abc123")]);

        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.Approved, build.State);
        Assert.Equal(approved.Id, build.LatestSubmissionId);
        Assert.Equal(At8.AddMinutes(5), build.LastApproval!.ApprovedAt);
        Assert.Equal("Me", build.LastApproval.ApprovedByName);
        Assert.Equal("abc123", build.LastApproval.ApprovedSha);
        Assert.Equal(FeatureBuildState.NotSubmitted, (await BuildOf(_hard)).State);
    }

    [Fact]
    public async Task Overview_SubmittedAgainAfterApproval_IsAnalyzing_AndKeepsTheEarlierApproval()
    {
        var earlier = Submission(_easy, BuildSubmissionStatus.Approved, At8);
        var newer = Submission(_easy, BuildSubmissionStatus.Analyzing, At8.AddHours(1));
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([newer]);
        _submissions.GetLatestApprovalPerFeatureAsync(_workspaceId).Returns(
            [new ApprovedSubmissionInfo(_easy.FeatureId, earlier.Id, At8.AddMinutes(5), _me.UserId, "Me", "abc123")]);

        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.Analyzing, build.State);
        Assert.Equal(newer.Id, build.LatestSubmissionId);
        Assert.Equal(earlier.Id, build.LastApproval!.SubmissionId);
    }

    [Fact]
    public async Task Overview_ApproverWithoutAccount_KeepsTheApprovalWithoutAName()
    {
        var approved = Submission(_easy, BuildSubmissionStatus.Approved, At8);
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([approved]);
        _submissions.GetLatestApprovalPerFeatureAsync(_workspaceId).Returns(
            [new ApprovedSubmissionInfo(_easy.FeatureId, approved.Id, At8, Guid.NewGuid(), null, "abc123")]);

        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.Approved, build.State);
        Assert.Null(build.LastApproval!.ApprovedByName);
    }

    [Fact]
    public async Task Overview_OpenDriftAlert_IsDriftUnresolved_AndPointsAtTheAlert()
    {
        var drift = Submission(_easy, BuildSubmissionStatus.Drift, At8);
        drift.ConfidenceScore = 0.40m;
        var older = new DriftAlert { FeatureId = _easy.FeatureId, Status = DriftAlertStatus.Resolved, CreatedAt = At8.AddHours(-1) };
        var open = new DriftAlert { FeatureId = _easy.FeatureId, Status = DriftAlertStatus.Acknowledged, CreatedAt = At8 };
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([drift]);
        _alerts.GetByWorkspaceIdAsync(_workspaceId, null).Returns([older, open]);

        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.DriftUnresolved, build.State);
        Assert.Equal(open.Id, build.OpenAlertId);
    }

    [Fact]
    public async Task Overview_DriftOverriddenByTheManager_IsReadyToApprove()
    {
        var drift = Submission(_easy, BuildSubmissionStatus.Drift, At8);
        drift.ConfidenceScore = 0.40m;
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([drift]);
        _alerts.GetByWorkspaceIdAsync(_workspaceId, null).Returns(
            [new DriftAlert { FeatureId = _easy.FeatureId, Status = DriftAlertStatus.Overridden, CreatedAt = At8 }]);

        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.ReadyToApprove, build.State);
        Assert.Null(build.OpenAlertId);
    }

    [Fact]
    public async Task Overview_FailedAnalysis_ShowsTheFailureCode()
    {
        var failed = Submission(_easy, BuildSubmissionStatus.Failed, At8);
        failed.FailureCode = "DIFF_TOO_LARGE";
        _submissions.GetLatestPerFeatureAsync(_workspaceId).Returns([failed]);

        var build = (await BuildOf(_easy));

        Assert.Equal(FeatureBuildState.Failed, build.State);
        Assert.Equal("DIFF_TOO_LARGE", build.FailureCode);
    }

    [Fact]
    public async Task Overview_ReadsBuildDataOnce_HoweverManyFeatures()
    {
        await _service.GetOverviewAsync(_me.UserId, _workspaceId);

        await _submissions.Received(1).GetLatestPerFeatureAsync(_workspaceId);
        await _submissions.Received(1).GetLatestApprovalPerFeatureAsync(_workspaceId);
        await _alerts.Received(1).GetByWorkspaceIdAsync(_workspaceId, null);
        await _submissions.DidNotReceive().GetLatestAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }
}
