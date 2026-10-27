using NSubstitute;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Tests.Application;

public class BuildServiceTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private readonly IWorkspaceAccessGuard _guard = Substitute.For<IWorkspaceAccessGuard>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IFeatureAssignmentRepository _assignments = Substitute.For<IFeatureAssignmentRepository>();
    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IGitHubAppService _app = Substitute.For<IGitHubAppService>();
    private readonly IBuildAnalysisQueue _queue = Substitute.For<IBuildAnalysisQueue>();
    private readonly IDirectPushDetector _detector = Substitute.For<IDirectPushDetector>();
    private readonly InMemoryAuditTrailRepository _auditEntries = new();
    private readonly AuditTrail _audit;
    private readonly Workspace _workspace = new()
    {
        GitHubRepoOwner = "clinic", GitHubRepoName = "booking", GitHubInstallationId = 4242
    };
    private readonly SnapshotFeature _feature = new()
    {
        FeatureId = Guid.NewGuid(), Name = "Booking", Status = SnapshotFeatureStatus.Locked
    };
    private readonly FeatureAssignment _assignment;
    private readonly BuildService _service;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _me = Guid.NewGuid();
    private readonly string _branch;
    private readonly string _staging;

    public BuildServiceTests()
    {
        _audit = new AuditTrail(_auditEntries);
        _service = new BuildService(_guard, _workspaces, _definitions, _assignments, _submissions, _app, _audit, _detector, _queue);
        _guard.GetWorkspaceAsMemberAsync(_me, _workspaceId).Returns(_workspace);

        _branch = $"feature/{_feature.FeatureId}-me-dev";
        _staging = $"staging/{_feature.FeatureId}-me-dev";

        var snapshot = new MvpSnapshot { WorkspaceId = _workspaceId, Version = 3 };
        snapshot.Features.Add(_feature);
        _definitions.GetLatestSnapshotAsync(_workspaceId).Returns(snapshot);

        _assignment = new FeatureAssignment
        {
            WorkspaceId = _workspaceId, FeatureId = _feature.FeatureId, AssignedUserId = _me, BranchName = _branch
        };
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns([_assignment]);

        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging).Returns(Sha);
        _submissions.GetAnalyzingAsync(_workspaceId, _feature.FeatureId).Returns([]);
    }

    private Task<BuildSubmissionDto> Submit(string? branch = null, string sha = Sha) =>
        _service.SubmitAsync(_me, _workspaceId, new SubmitBuildRequest { BranchName = branch ?? _branch, CommitSha = sha });

    private async Task AssertRejected<TException>(Func<Task> submit) where TException : AppException
    {
        await Assert.ThrowsAsync<TException>(submit);
        await _submissions.DidNotReceive().AddAsync(Arg.Any<BuildSubmission>());
    }

    // ---- accepted ----

    [Fact]
    public async Task Submit_OfTheHeldFeature_IsRecordedForAnalysis()
    {
        var result = await Submit();

        Assert.Equal(BuildSubmissionStatus.Analyzing, result.Status);
        Assert.Equal(_feature.FeatureId, result.FeatureId);
        Assert.Equal(_staging, result.StagingBranch);
        Assert.Equal(Sha, result.HeadSha);
        Assert.Equal(3, result.SnapshotVersion);
        await _submissions.Received(1).AddAsync(Arg.Is<BuildSubmission>(s =>
            s.WorkspaceId == _workspaceId && s.FeatureId == _feature.FeatureId && s.SubmittedByUserId == _me));
        await _submissions.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_QueuesTheAnalysisOfTheNewSubmission()
    {
        var result = await Submit();

        _queue.Received(1).Enqueue(result.Id, _feature.FeatureId);
    }

    [Fact]
    public async Task Submit_ReplacesTheOlderSubmissionsOfTheSameFeatureStillWaiting()
    {
        var older = new BuildSubmission { FeatureId = _feature.FeatureId };
        _submissions.GetAnalyzingAsync(_workspaceId, _feature.FeatureId).Returns([older]);

        await Submit();

        Assert.Equal(BuildSubmissionStatus.Superseded, older.Status);
    }

    [Fact]
    public async Task Submit_LooksForCodeThatBypassedShipMate_BeforeRecordingTheSubmission()
    {
        await Submit();

        await _detector.Received(1).DetectAsync(_workspace, _assignment);
    }

    [Fact]
    public async Task Submit_Refused_DoesNotRunTheDetection()
    {
        await Assert.ThrowsAsync<CommitNotInStagingException>(
            () => _service.SubmitAsync(_me, _workspaceId, new SubmitBuildRequest
            {
                BranchName = _branch, CommitSha = "9999999999999999999999999999999999999999"
            }));

        await _detector.DidNotReceiveWithAnyArgs().DetectAsync(default!, default!);
    }

    [Fact]
    public async Task Submit_RefusedSubmits_QueueNothing()
    {
        await Assert.ThrowsAsync<ProtectedBranchSubmitException>(() => Submit("main"));

        _queue.DidNotReceiveWithAnyArgs().Enqueue(default, default);
    }

    [Fact]
    public async Task Submit_CommitHashInUpperCase_IsStillTheSameCommit()
    {
        var result = await Submit(sha: Sha.ToUpperInvariant());

        Assert.Equal(Sha, result.HeadSha);
    }

    // ---- the branch ----

    [Theory]
    [InlineData("main")]
    [InlineData("develop")]
    [InlineData("release/1.0")]
    public async Task Submit_FromASharedBranch_IsRefused(string branch) =>
        await AssertRejected<ProtectedBranchSubmitException>(() => Submit(branch));

    [Fact]
    public async Task Submit_FromAHotfixBranch_IsNotSupportedYet() =>
        await AssertRejected<HotfixNotSupportedException>(() => Submit("hotfix/42-me-dev"));

    [Theory]
    [InlineData("my-branch")]
    [InlineData("feature/not-a-guid-me-dev")]
    public async Task Submit_FromABranchThatIsNotAFeatureBranch_HasAnInvalidFeatureId(string branch) =>
        await AssertRejected<InvalidFeatureIdException>(() => Submit(branch));

    [Fact]
    public async Task Submit_FeatureThatIsNotInTheSnapshot_HasAnInvalidFeatureId() =>
        await AssertRejected<InvalidFeatureIdException>(() => Submit($"feature/{Guid.NewGuid()}-me-dev"));

    [Fact]
    public async Task Submit_FromTheRightFeatureButADifferentBranchName_HasAnInvalidFeatureId() =>
        await AssertRejected<InvalidFeatureIdException>(() => Submit($"feature/{_feature.FeatureId}-someone-else"));

    // ---- the feature ----

    [Fact]
    public async Task Submit_FeatureRemovedFromTheScope_IsRefused()
    {
        _feature.Status = SnapshotFeatureStatus.Replaced;

        await AssertRejected<FeatureReplacedException>(() => Submit());
    }

    [Fact]
    public async Task Submit_BackupFeature_IsRefused()
    {
        _feature.Status = SnapshotFeatureStatus.Backup;

        await AssertRejected<FeatureIsBackupException>(() => Submit());
    }

    [Fact]
    public async Task Submit_FeatureNobodyHolds_IsRefused()
    {
        _assignments.GetByWorkspaceIdAsync(_workspaceId).Returns([]);

        await AssertRejected<FeatureNotAssignedException>(() => Submit());
    }

    [Fact]
    public async Task Submit_FeatureWhoseAssignmentWasReleased_CountsAsNotHeld()
    {
        _assignment.ReleasedAt = DateTime.UtcNow;

        await AssertRejected<FeatureNotAssignedException>(() => Submit());
    }

    [Fact]
    public async Task Submit_FeatureAnotherDeveloperHolds_IsRefused()
    {
        _assignment.AssignedUserId = Guid.NewGuid();

        await AssertRejected<FeatureAssignedToOtherException>(() => Submit());
    }

    // ---- the commit ----

    [Fact]
    public async Task Submit_CommitThatIsNotTheStagingHead_IsRefused()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging).Returns("f".PadLeft(40, 'f'));

        await AssertRejected<CommitNotInStagingException>(() => Submit());
    }

    [Fact]
    public async Task Submit_WhenTheStagingBranchDoesNotExist_IsRefused()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging).Returns((string?)null);

        await AssertRejected<CommitNotInStagingException>(() => Submit());
    }

    // ---- the repo ----

    [Fact]
    public async Task Submit_WithoutALinkedRepo_IsRefused()
    {
        _workspace.GitHubInstallationId = null;

        await AssertRejected<WorkspaceRepoNotLinkedException>(() => Submit());
    }

    // ---- governance ----

    [Fact]
    public async Task Submit_WhenTheAppWasUninstalled_SuspendsGovernanceAndRefuses()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging)
            .Returns<string?>(_ => throw new GitHubAppUninstalledException());

        await AssertRejected<GovernanceSuspendedException>(() => Submit());

        Assert.NotNull(_workspace.GovernanceSuspendedAt);
        Assert.Equal("APP_UNINSTALLED", _workspace.GovernanceSuspendedReason);
        await _workspaces.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_WhenTheAppWasUninstalled_WritesASystemAlertToTheAuditTrail()
    {
        _app.GetBranchHeadShaAsync(4242, "clinic", "booking", _staging)
            .Returns<string?>(_ => throw new GitHubAppUninstalledException());

        await AssertRejected<GovernanceSuspendedException>(() => Submit());

        var record = Assert.Single(_auditEntries.Entries);
        Assert.Equal(AuditTypes.SystemAlert, record.AuditType);
        Assert.Equal(_workspace.Id, record.WorkspaceId);
        Assert.Null(record.ActorUserId);
        Assert.Contains("APP_UNINSTALLED", record.Payload);
    }

    [Fact]
    public async Task Submit_WhileSuspendedAndTheAppIsStillMissing_IsRefusedWithoutTouchingTheBranch()
    {
        _workspace.GovernanceSuspendedAt = DateTime.UtcNow;
        _app.FindInstallationAsync("clinic", "booking").Returns((GitHubAppInstallation?)null);

        await AssertRejected<GovernanceSuspendedException>(() => Submit());

        await _app.DidNotReceiveWithAnyArgs().GetBranchHeadShaAsync(default, default!, default!, default!);
        Assert.NotNull(_workspace.GovernanceSuspendedAt);
    }

    [Fact]
    public async Task Submit_WhileSuspendedButTheAppIsBack_ResumesGovernanceAndGoesThrough()
    {
        _workspace.GovernanceSuspendedAt = DateTime.UtcNow;
        _workspace.GovernanceSuspendedReason = "APP_UNINSTALLED";
        _app.FindInstallationAsync("clinic", "booking").Returns(new GitHubAppInstallation(9999, IsOrganization: true));
        _app.GetBranchHeadShaAsync(9999, "clinic", "booking", _staging).Returns(Sha);

        var result = await Submit();

        Assert.Equal(BuildSubmissionStatus.Analyzing, result.Status);
        Assert.Null(_workspace.GovernanceSuspendedAt);
        Assert.Null(_workspace.GovernanceSuspendedReason);
        Assert.Equal(9999, _workspace.GitHubInstallationId);
        Assert.True(_workspace.GitHubRepoIsOrganization);
    }

    // ---- reading a result ----

    [Fact]
    public async Task GetSubmission_ShowsTheVerdictWithItsFindingsAndSuggestions()
    {
        var suspected = Guid.NewGuid();
        var submission = new BuildSubmission
        {
            WorkspaceId = _workspaceId, FeatureId = _feature.FeatureId, Status = BuildSubmissionStatus.Drift,
            ConfidenceScore = 0.4m, ThresholdAtCheck = 0.75m, SuspectedFeatureIds = [suspected],
            FindingsJson = "[{\"file\":\"src/pay.ts\",\"description\":\"Builds payments\"}]",
            SuggestionsJson = "[{\"file\":\"a.ts\",\"problem\":\"p\",\"suggestion\":\"s\",\"reason\":\"r\"}]"
        };
        _submissions.GetByIdAsync(submission.Id).Returns(submission);

        var result = await _service.GetSubmissionAsync(_me, _workspaceId, submission.Id);

        Assert.Equal(BuildSubmissionStatus.Drift, result.Status);
        Assert.Equal(0.4m, result.ConfidenceScore);
        Assert.Equal([suspected], result.SuspectedFeatureIds);
        Assert.Equal("src/pay.ts", Assert.Single(result.Findings).File);
        Assert.Equal("Builds payments", result.Findings[0].Description);
        Assert.Equal("r", Assert.Single(result.Suggestions).Reason);
    }

    [Fact]
    public async Task GetSubmission_BeforeTheAnalysisFinishes_HasNoVerdictYet()
    {
        var submission = new BuildSubmission { WorkspaceId = _workspaceId, FeatureId = _feature.FeatureId };
        _submissions.GetByIdAsync(submission.Id).Returns(submission);

        var result = await _service.GetSubmissionAsync(_me, _workspaceId, submission.Id);

        Assert.Equal(BuildSubmissionStatus.Analyzing, result.Status);
        Assert.Null(result.ConfidenceScore);
        Assert.Empty(result.Findings);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task GetSubmission_OfAnotherWorkspace_IsNotFound()
    {
        var submission = new BuildSubmission { WorkspaceId = Guid.NewGuid() };
        _submissions.GetByIdAsync(submission.Id).Returns(submission);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetSubmissionAsync(_me, _workspaceId, submission.Id));
    }

    [Fact]
    public async Task GetSubmission_Unknown_IsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetSubmissionAsync(_me, _workspaceId, Guid.NewGuid()));
}
