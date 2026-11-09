using NSubstitute;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class NextBestActionServiceTests
{
    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IWorkspaceMemberRepository _members = Substitute.For<IWorkspaceMemberRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly NextBestActionService _service;

    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();
    private readonly MvpSnapshot _snapshot;
    private readonly List<FeatureAssignment> _assignmentList = new();
    private readonly List<DriftAlert> _openAlerts = new();

    public NextBestActionServiceTests()
    {
        _service = new NextBestActionService(_guard, _definitions, _members, _assignments, _submissions, _alerts);

        _snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 2 };
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(_snapshot);
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns(_assignmentList);
        _alerts.GetOpenByWorkspaceAsync(_workspaceId).Returns(_openAlerts);
        _members.GetByWorkspaceAndUserIdAsync(_workspaceId, _me).Returns(new WorkspaceMember { SkillLevel = 3 });
    }

    private SnapshotFeature Feature(string name, int complexity = 2, SnapshotFeatureStatus status = SnapshotFeatureStatus.Locked)
    {
        var feature = new SnapshotFeature { FeatureId = Guid.NewGuid(), Name = name, Complexity = complexity, Status = status };
        _snapshot.Features.Add(feature);
        return feature;
    }

    private void DependsOn(SnapshotFeature feature, SnapshotFeature prerequisite) =>
        _snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = feature.FeatureId, DependsOnFeatureId = prerequisite.FeatureId });

    private void Assign(SnapshotFeature feature, Guid user, bool released = false) =>
        _assignmentList.Add(new FeatureAssignment
        {
            FeatureId = feature.FeatureId, AssignedUserId = user, ReleasedAt = released ? DateTime.UtcNow : null
        });

    private DriftAlert OpenAlert(SnapshotFeature feature, DriftAlertStatus status = DriftAlertStatus.Unresolved)
    {
        var alert = new DriftAlert { FeatureId = feature.FeatureId, Status = status };
        _openAlerts.Add(alert);
        return alert;
    }

    private BuildSubmission Submission(
        SnapshotFeature feature, BuildSubmissionStatus status, decimal score = 0.9m, DriftAlertStatus? latestAlert = null)
    {
        var submission = new BuildSubmission
        {
            WorkspaceId = _workspaceId, FeatureId = feature.FeatureId, Status = status,
            ConfidenceScore = score, ThresholdAtCheck = 0.75m
        };
        _submissions.GetLatestAsync(_workspaceId, feature.FeatureId).Returns(submission);
        if (latestAlert is not null)
        {
            _alerts.GetLatestByFeatureAsync(_workspaceId, feature.FeatureId).Returns(new DriftAlert { Status = latestAlert.Value });
        }

        return submission;
    }

    private Task<NextBestActionDto> Ask() => _service.GetAsync(_me, _workspaceId);

    // ---- nothing to go on ----

    [Fact]
    public async Task BeforeTheFirstLock_ThereIsNothingToSuggest()
    {
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns((MvpSnapshot?)null);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    [Fact]
    public async Task WithNoFeatureOfYoursAndNothingFreeToTake_ThereIsNothingToSuggest()
    {
        var theirs = Feature("Payments");
        Assign(theirs, _other);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    // ---- 1. your own drift ----

    [Fact]
    public async Task DriftOnYourFeature_ComesFirstAndNamesTheAlert()
    {
        var mine = Feature("Booking");
        Assign(mine, _me);
        var alert = OpenAlert(mine, DriftAlertStatus.Acknowledged);
        Submission(mine, BuildSubmissionStatus.InScope);

        var result = await Ask();

        Assert.Equal(NextBestActionType.FixDrift, result.Action);
        Assert.Equal(mine.FeatureId, result.FeatureId);
        Assert.Equal("Booking", result.FeatureName);
        Assert.Equal(alert.Id, result.AlertId);
    }

    [Fact]
    public async Task DriftOnSomeoneElsesFeature_IsNotYourNextAction()
    {
        var mine = Feature("Booking");
        var theirs = Feature("Payments");
        Assign(mine, _me);
        Assign(theirs, _other);
        OpenAlert(theirs);

        Assert.Equal(NextBestActionType.WorkOnFeature, (await Ask()).Action);
    }

    // ---- 2. approving ----

    [Theory]
    [InlineData(BuildSubmissionStatus.InScope)]
    [InlineData(BuildSubmissionStatus.SkippedMaintenance)]
    public async Task AFeatureThatPassedTheCheck_IsWaitingForYourApproval(BuildSubmissionStatus status)
    {
        var mine = Feature("Booking");
        Assign(mine, _me);
        var submission = Submission(mine, status);

        var result = await Ask();

        Assert.Equal(NextBestActionType.ApproveSubmission, result.Action);
        Assert.Equal(submission.Id, result.SubmissionId);
        Assert.Equal(mine.FeatureId, result.FeatureId);
    }

    [Fact]
    public async Task ADriftTheManagerOverrode_IsWaitingForYourApproval()
    {
        var mine = Feature("Booking");
        Assign(mine, _me);
        Submission(mine, BuildSubmissionStatus.Drift, score: 0.4m, latestAlert: DriftAlertStatus.Overridden);

        Assert.Equal(NextBestActionType.ApproveSubmission, (await Ask()).Action);
    }

    [Fact]
    public async Task ADriftNobodyOverrode_CannotBeApproved()
    {
        var mine = Feature("Booking");
        Assign(mine, _me);
        Submission(mine, BuildSubmissionStatus.Drift, score: 0.4m, latestAlert: DriftAlertStatus.Resolved);

        Assert.NotEqual(NextBestActionType.ApproveSubmission, (await Ask()).Action);
    }

    [Fact]
    public async Task ApprovingComesBeforeStartingOnAnotherFeature()
    {
        var done = Feature("Booking");
        var fresh = Feature("Reports");
        Assign(done, _me);
        Assign(fresh, _me);
        Submission(done, BuildSubmissionStatus.InScope);

        Assert.Equal(NextBestActionType.ApproveSubmission, (await Ask()).Action);
    }

    // ---- 3. your own work ----

    [Fact]
    public async Task AFeatureYouHaveNotStarted_IsWhatToWorkOn()
    {
        var mine = Feature("Booking");
        Assign(mine, _me);

        var result = await Ask();

        Assert.Equal(NextBestActionType.WorkOnFeature, result.Action);
        Assert.Equal(mine.FeatureId, result.FeatureId);
    }

    [Theory]
    [InlineData(BuildSubmissionStatus.Failed)]
    [InlineData(BuildSubmissionStatus.Analyzing)]
    public async Task AFeatureWhoseLastSubmitGotNoVerdict_StillHasWorkInIt(BuildSubmissionStatus status)
    {
        var mine = Feature("Booking");
        Assign(mine, _me);
        Submission(mine, status);

        Assert.Equal(NextBestActionType.WorkOnFeature, (await Ask()).Action);
    }

    [Fact]
    public async Task ApprovedCode_IsDoneUntilYouSubmitMore()
    {
        var mine = Feature("Booking");
        Assign(mine, _me);
        Submission(mine, BuildSubmissionStatus.Approved);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    [Fact]
    public async Task OfSeveralFeaturesToWorkOn_TheFirstByNameIsChosen()
    {
        var b = Feature("Reports");
        var a = Feature("Booking");
        Assign(b, _me);
        Assign(a, _me);

        Assert.Equal(a.FeatureId, (await Ask()).FeatureId);
    }

    [Fact]
    public async Task AFeatureYouNoLongerHold_IsIgnored()
    {
        var released = Feature("Booking");
        Assign(released, _me, released: true);

        Assert.NotEqual(NextBestActionType.WorkOnFeature, (await Ask()).Action);
    }

    [Fact]
    public async Task AFeatureRemovedFromTheScope_IsIgnored()
    {
        var removed = Feature("Booking", status: SnapshotFeatureStatus.Replaced);
        Assign(removed, _me);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    // ---- blocked ----

    [Fact]
    public async Task AFeatureWaitingForABackupFeature_IsBlockedWithThatReason()
    {
        var backup = Feature("Auth", status: SnapshotFeatureStatus.Backup);
        var mine = Feature("Booking");
        DependsOn(mine, backup);
        Assign(mine, _me);

        var result = await Ask();

        Assert.Equal(NextBestActionType.Blocked, result.Action);
        Assert.Equal("dependency_pending_assignment", result.BlockedReason);
        Assert.Equal(backup.FeatureId, result.BlockedByFeatureId);
        Assert.Equal(mine.FeatureId, result.FeatureId);
    }

    [Fact]
    public async Task AFeatureWaitingForAFeatureWithUnresolvedDrift_IsBlockedWithThatReason()
    {
        var prerequisite = Feature("Auth");
        var mine = Feature("Booking");
        DependsOn(mine, prerequisite);
        Assign(prerequisite, _other);
        Assign(mine, _me);
        OpenAlert(prerequisite);

        var result = await Ask();

        Assert.Equal(NextBestActionType.Blocked, result.Action);
        Assert.Equal("dependency_drift_unresolved", result.BlockedReason);
        Assert.Equal(prerequisite.FeatureId, result.BlockedByFeatureId);
    }

    [Fact]
    public async Task AnAcknowledgedAlert_StillBlocksThoseWhoDependOnTheFeature()
    {
        var prerequisite = Feature("Auth");
        var mine = Feature("Booking");
        DependsOn(mine, prerequisite);
        Assign(prerequisite, _other);
        Assign(mine, _me);
        OpenAlert(prerequisite, DriftAlertStatus.Acknowledged);

        Assert.Equal("dependency_drift_unresolved", (await Ask()).BlockedReason);
    }

    [Fact]
    public async Task WhenBothApply_TheBackupIsReportedFirst()
    {
        var backup = Feature("Auth", status: SnapshotFeatureStatus.Backup);
        var drifting = Feature("Profile");
        var mine = Feature("Booking");
        DependsOn(mine, drifting);
        DependsOn(mine, backup);
        Assign(drifting, _other);
        Assign(mine, _me);
        OpenAlert(drifting);

        Assert.Equal("dependency_pending_assignment", (await Ask()).BlockedReason);
    }

    [Fact]
    public async Task ABlockedFeatureDoesNotStopYouWorkingOnAnother()
    {
        var backup = Feature("Auth", status: SnapshotFeatureStatus.Backup);
        var blocked = Feature("Booking");
        var free = Feature("Reports");
        DependsOn(blocked, backup);
        Assign(blocked, _me);
        Assign(free, _me);

        var result = await Ask();

        Assert.Equal(NextBestActionType.WorkOnFeature, result.Action);
        Assert.Equal(free.FeatureId, result.FeatureId);
    }

    [Fact]
    public async Task AFreeFeatureToTake_IsOfferedBeforeReportingABlock()
    {
        var backup = Feature("Auth", status: SnapshotFeatureStatus.Backup);
        var blocked = Feature("Booking");
        var free = Feature("Reports");
        DependsOn(blocked, backup);
        Assign(blocked, _me);

        var result = await Ask();

        Assert.Equal(NextBestActionType.ClaimFeature, result.Action);
        Assert.Equal(free.FeatureId, result.FeatureId);
    }

    // ---- 4. taking a free feature ----

    [Fact]
    public async Task AFreeFeatureYourSkillCovers_IsOffered()
    {
        var free = Feature("Reports", complexity: 3);

        var result = await Ask();

        Assert.Equal(NextBestActionType.ClaimFeature, result.Action);
        Assert.Equal(free.FeatureId, result.FeatureId);
    }

    [Fact]
    public async Task AFeatureAboveYourSkill_IsNotOffered()
    {
        Feature("Reports", complexity: 4);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    [Fact]
    public async Task WithoutASkillLevel_NothingIsOffered()
    {
        _members.GetByWorkspaceAndUserIdAsync(_workspaceId, _me).Returns(new WorkspaceMember { SkillLevel = null });
        Feature("Reports", complexity: 1);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    [Fact]
    public async Task ATakenOrBackupFeature_IsNotOffered()
    {
        var taken = Feature("Payments");
        Assign(taken, _other);
        Feature("Auth", status: SnapshotFeatureStatus.Backup);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }

    [Fact]
    public async Task AFeatureYouReleasedByLosingIt_IsFreeToTakeAgain()
    {
        var released = Feature("Booking");
        Assign(released, _other, released: true);

        Assert.Equal(NextBestActionType.ClaimFeature, (await Ask()).Action);
    }

    [Fact]
    public async Task TheFreeFeatureMostOthersWaitFor_IsOfferedFirst()
    {
        var base1 = Feature("Zeta");
        var other = Feature("Alpha");
        var dependent1 = Feature("D1");
        var dependent2 = Feature("D2");
        DependsOn(dependent1, base1);
        DependsOn(dependent2, base1);
        Assign(dependent1, _other);
        Assign(dependent2, _other);

        Assert.Equal(base1.FeatureId, (await Ask()).FeatureId);
        Assert.NotEqual(other.FeatureId, (await Ask()).FeatureId);
    }

    [Fact]
    public async Task AFreeFeatureWhoseOwnPrerequisiteIsBlocked_IsNotOffered()
    {
        var backup = Feature("Auth", status: SnapshotFeatureStatus.Backup);
        var waiting = Feature("Booking");
        DependsOn(waiting, backup);

        Assert.Equal(NextBestActionType.NoAction, (await Ask()).Action);
    }
}
