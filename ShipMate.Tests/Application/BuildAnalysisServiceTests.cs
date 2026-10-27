using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ShipMate.Application.DTOs.Ai;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Tests.Application;

public class BuildAnalysisServiceTests
{
    private const string Head = "1111111111111111111111111111111111111111";
    private const string OldHead = "2222222222222222222222222222222222222222";

    private readonly IBuildSubmissionRepository _submissions = Substitute.For<IBuildSubmissionRepository>();
    private readonly IDriftAlertRepository _alerts = Substitute.For<IDriftAlertRepository>();
    private readonly IWorkspaceRepository _workspaces = Substitute.For<IWorkspaceRepository>();
    private readonly IProductDefinitionRepository _definitions = Substitute.For<IProductDefinitionRepository>();
    private readonly IGitHubAppService _github = Substitute.For<IGitHubAppService>();
    private readonly IAiGateway _ai = Substitute.For<IAiGateway>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly IWorkspaceNotifier _notifier = Substitute.For<IWorkspaceNotifier>();
    private readonly Workspace _workspace = new()
    {
        GitHubRepoOwner = "clinic", GitHubRepoName = "booking", GitHubInstallationId = 4242, DriftConfidenceThreshold = 0.75m
    };
    private readonly SnapshotFeature _mine = new()
    {
        FeatureId = Guid.NewGuid(), Name = "Booking", Description = "Patients book visits", Status = SnapshotFeatureStatus.Locked
    };
    private readonly SnapshotFeature _other = new()
    {
        FeatureId = Guid.NewGuid(), Name = "Payments", Description = "Pay online", Status = SnapshotFeatureStatus.Locked
    };
    private readonly MvpSnapshot _snapshot;
    private readonly BuildSubmission _submission;
    private readonly BuildAnalysisService _service;
    private AiJsonRequest? _aiRequest;

    public BuildAnalysisServiceTests()
    {
        _service = new BuildAnalysisService(
            _submissions, _alerts, _workspaces, _definitions, _github, _ai, _configuration,
            _notifier, Substitute.For<ILogger<BuildAnalysisService>>());

        _snapshot = new MvpSnapshot { WorkspaceId = _workspace.Id, Version = 3 };
        _snapshot.Features.Add(_mine);
        _snapshot.Features.Add(_other);
        _snapshot.Dependencies.Add(new SnapshotDependency { FeatureId = _other.FeatureId, DependsOnFeatureId = _mine.FeatureId });
        _definitions.GetLatestSnapshotAsync(_workspace.Id).Returns(_snapshot);
        _workspaces.GetByIdAsync(_workspace.Id).Returns(_workspace);
        _alerts.GetOpenByFeatureAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(new List<DriftAlert>());

        _submission = new BuildSubmission
        {
            WorkspaceId = _workspace.Id, FeatureId = _mine.FeatureId, HeadSha = Head, SnapshotVersion = 3
        };
        _submissions.GetByIdAsync(_submission.Id).Returns(_submission);
        _submissions.IsAnalyzingAsync(_submission.Id).Returns(true);

        _ai.Model.Returns("test-model");
        _github.GetDefaultBranchAsync(4242, "clinic", "booking").Returns("main");
        GivenDiffAgainstMain(new ChangedFile("src/booking.ts", "added", "@@ -0,0 +1 @@\n+book();"));
    }

    private void GivenDiffAgainstMain(params ChangedFile[] files) =>
        _github.CompareAsync(4242, "clinic", "booking", "main", Head)
            .Returns(new GitHubComparison(["add booking"], files, Truncated: false));

    private void GivenAiAnswers(decimal score, string[]? suspected = null, bool withFinding = false) =>
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _aiRequest = call.Arg<AiJsonRequest>();
            return new AiJsonResponse
            {
                Model = "gemini-test",
                Json = JsonSerializer.Serialize(new
                {
                    confidence_score = score,
                    suspected_feature_ids = suspected ?? [],
                    findings = withFinding ? new[] { new { file = "src/pay.ts", description = "Builds payments" } } : [],
                    suggestions = new[] { new { file = "src/booking.ts", problem = "Factory with one use", suggestion = "Call it directly", reason = "Fewer layers" } }
                })
            };
        });

    private Task Analyze(CancellationToken cancellationToken = default) =>
        _service.AnalyzeAsync(_submission.Id, cancellationToken);

    // ---- the AI verdict ----

    [Fact]
    public async Task CodeInsideTheScope_IsInScopeWithTheSuggestions()
    {
        GivenAiAnswers(0.92m);

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.InScope, _submission.Status);
        Assert.Equal(0.92m, _submission.ConfidenceScore);
        Assert.Equal(0.75m, _submission.ThresholdAtCheck);
        Assert.Empty(_submission.SuspectedFeatureIds);
        Assert.Contains("Factory with one use", _submission.SuggestionsJson);
        Assert.Equal("gemini-test", _submission.AiModel);
        Assert.NotNull(_submission.AiRawResponse);
        Assert.NotNull(_submission.AnalyzedAt);
        await _submissions.Received().SaveChangesAsync();
    }

    [Fact]
    public async Task CodeBeyondTheScope_IsDriftWithTheSuspectedFeatures()
    {
        GivenAiAnswers(0.40m, [_other.FeatureId.ToString()], withFinding: true);

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Drift, _submission.Status);
        Assert.Equal([_other.FeatureId], _submission.SuspectedFeatureIds);
        Assert.Contains("Builds payments", _submission.FindingsJson);
    }

    [Theory]
    [InlineData(0.75, BuildSubmissionStatus.InScope)]
    [InlineData(0.74, BuildSubmissionStatus.Drift)]
    public async Task TheThreshold_DecidesAtTheBoundary(double score, BuildSubmissionStatus expected)
    {
        GivenAiAnswers((decimal)score);

        await Analyze();

        Assert.Equal(expected, _submission.Status);
    }

    [Fact]
    public async Task ARaisedThreshold_IsUsedAndRecorded()
    {
        _workspace.DriftConfidenceThreshold = 0.85m;
        GivenAiAnswers(0.80m);

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Drift, _submission.Status);
        Assert.Equal(0.85m, _submission.ThresholdAtCheck);
    }

    [Fact]
    public async Task TheAiSeesTheFeatureTheOtherFeaturesTheDependenciesAndTheDiff()
    {
        GivenAiAnswers(0.9m);

        await Analyze();

        var prompt = _aiRequest!.UserPrompt;
        Assert.Contains(_mine.FeatureId.ToString(), prompt);
        Assert.Contains("Patients book visits", prompt);
        Assert.Contains("Pay online", prompt);
        Assert.Contains("depends_on_feature_id", prompt);
        Assert.Contains("book();", prompt);
        await _ai.Received(1).GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheScopeIsCheckedAgainstTheLatestSnapshot()
    {
        _snapshot.Version = 5;
        GivenAiAnswers(0.9m);

        await Analyze();

        Assert.Equal(5, _submission.SnapshotVersion);
    }

    // ---- real-time messages ----

    private Task Heard(string eventName) =>
        _notifier.Received(1).PublishAsync(_workspace.Id, eventName, Arg.Any<object>());

    private Task NotHeard(string eventName) =>
        _notifier.DidNotReceive().PublishAsync(Arg.Any<Guid>(), eventName, Arg.Any<object>());

    [Fact]
    public async Task AnInScopeVerdict_IsAnnouncedWithItsSuggestions()
    {
        GivenAiAnswers(0.92m);

        await Analyze();

        await Heard(WorkspaceEvents.AnalysisCompleted);
        await Heard(WorkspaceEvents.OptimizationSuggested);
        await NotHeard(WorkspaceEvents.DriftAlertTriggered);
    }

    [Fact]
    public async Task ADriftVerdict_IsAnnouncedWithItsAlert()
    {
        GivenAiAnswers(0.40m, [_other.FeatureId.ToString()]);

        await Analyze();

        await Heard(WorkspaceEvents.AnalysisCompleted);
        await Heard(WorkspaceEvents.DriftAlertTriggered);
        await NotHeard(WorkspaceEvents.OptimizationSuggested);
    }

    [Fact]
    public async Task AnInScopeVerdictWithoutSuggestions_SaysNothingAboutOptimizing()
    {
        GivenDiffAgainstMain();

        await Analyze();

        await Heard(WorkspaceEvents.AnalysisCompleted);
        await NotHeard(WorkspaceEvents.OptimizationSuggested);
    }

    [Fact]
    public async Task AFailedAnalysis_IsAnnouncedAsFinishedAndNothingElse()
    {
        _github.CompareAsync(4242, "clinic", "booking", "main", Head)
            .Returns(new GitHubComparison(["big"], [new ChangedFile("a.ts", "added", "x")], Truncated: true));

        await Analyze();

        await Heard(WorkspaceEvents.AnalysisCompleted);
        await NotHeard(WorkspaceEvents.DriftAlertTriggered);
        await NotHeard(WorkspaceEvents.OptimizationSuggested);
    }

    [Fact]
    public async Task AReplacedAnalysis_AnnouncesNothing()
    {
        _submissions.IsAnalyzingAsync(_submission.Id).Returns(false);
        GivenAiAnswers(0.30m);

        await Analyze();

        await _notifier.DidNotReceiveWithAnyArgs().PublishAsync(default, default!, default!);
    }

    // ---- drift alerts ----

    private DriftAlert GivenOpenAlert(DriftAlertStatus status = DriftAlertStatus.Unresolved)
    {
        var alert = new DriftAlert
        {
            WorkspaceId = _workspace.Id, FeatureId = _mine.FeatureId, SubmissionId = Guid.NewGuid(), Status = status
        };
        _alerts.GetOpenByFeatureAsync(_workspace.Id, _mine.FeatureId).Returns(new List<DriftAlert> { alert });
        return alert;
    }

    [Fact]
    public async Task ADrift_RaisesAnUnresolvedAlertThatKeepsWhatTheCheckFound()
    {
        GivenAiAnswers(0.40m, [_other.FeatureId.ToString()], withFinding: true);
        DriftAlert? raised = null;
        await _alerts.AddAsync(Arg.Do<DriftAlert>(a => raised = a));

        await Analyze();

        Assert.NotNull(raised);
        Assert.Equal(DriftAlertStatus.Unresolved, raised!.Status);
        Assert.Equal(_workspace.Id, raised.WorkspaceId);
        Assert.Equal(_mine.FeatureId, raised.FeatureId);
        Assert.Equal(_submission.Id, raised.SubmissionId);
        Assert.Equal(3, raised.SnapshotVersionAtCheck);
        Assert.Equal(0.75m, raised.ThresholdAtCheck);
        Assert.Equal(0.40m, raised.ConfidenceScore);
        Assert.Equal([_other.FeatureId], raised.SuspectedFeatureIds);
    }

    [Fact]
    public async Task AnInScopeResult_RaisesNoAlert()
    {
        GivenAiAnswers(0.92m);

        await Analyze();

        await _alerts.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Theory]
    [InlineData(DriftAlertStatus.Unresolved)]
    [InlineData(DriftAlertStatus.Acknowledged)]
    public async Task AnInScopeResult_ResolvesTheOpenAlertsOfTheFeature(DriftAlertStatus status)
    {
        var open = GivenOpenAlert(status);
        GivenAiAnswers(0.92m);

        await Analyze();

        Assert.Equal(DriftAlertStatus.Resolved, open.Status);
        Assert.Equal(_submission.Id, open.ResolvedBySubmissionId);
    }

    [Fact]
    public async Task ANewDrift_ReplacesTheOpenAlertWithANewOne()
    {
        var old = GivenOpenAlert(DriftAlertStatus.Acknowledged);
        GivenAiAnswers(0.30m);

        await Analyze();

        Assert.Equal(DriftAlertStatus.Resolved, old.Status);
        Assert.Equal(_submission.Id, old.ResolvedBySubmissionId);
        await _alerts.Received(1).AddAsync(Arg.Is<DriftAlert>(a => a.SubmissionId == _submission.Id));
    }

    [Fact]
    public async Task AFailedAnalysis_LeavesTheAlertsAlone()
    {
        var open = GivenOpenAlert();
        _github.CompareAsync(4242, "clinic", "booking", "main", Head)
            .Returns(new GitHubComparison(["big"], [new ChangedFile("a.ts", "added", "x")], Truncated: true));

        await Analyze();

        Assert.Equal(DriftAlertStatus.Unresolved, open.Status);
        await _alerts.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task ASupersededAnalysis_RaisesNoAlert()
    {
        _submissions.IsAnalyzingAsync(_submission.Id).Returns(false);
        GivenAiAnswers(0.30m);

        await Analyze();

        await _alerts.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    // ---- nothing to judge, or too much ----

    [Fact]
    public async Task NoDifferenceFromMain_IsInScopeWithoutTheAi()
    {
        GivenDiffAgainstMain();

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.InScope, _submission.Status);
        Assert.Equal(1m, _submission.ConfidenceScore);
        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }

    [Fact]
    public async Task ADiffGitHubCouldNotListCompletely_FailsAsTooLarge()
    {
        _github.CompareAsync(4242, "clinic", "booking", "main", Head)
            .Returns(new GitHubComparison(["big"], [new ChangedFile("a.ts", "added", "x")], Truncated: true));

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Failed, _submission.Status);
        Assert.Equal("DIFF_TOO_LARGE", _submission.FailureCode);
        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }

    [Fact]
    public async Task ADiffBeyondTheConfiguredSize_FailsAsTooLargeInsteadOfBeingCut()
    {
        _configuration["Build:MaxDiffCharacters"].Returns("20");
        GivenDiffAgainstMain(new ChangedFile("a.ts", "added", new string('x', 21)));

        await Analyze();

        Assert.Equal("DIFF_TOO_LARGE", _submission.FailureCode);
        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }

    [Fact]
    public async Task WhenGitHubKnowsNothingOfTheCommit_TheSubmissionFails()
    {
        _github.CompareAsync(4242, "clinic", "booking", "main", Head).Returns((GitHubComparison?)null);

        await Analyze();

        Assert.Equal("GITHUB_REQUEST_FAILED", _submission.FailureCode);
    }

    [Fact]
    public async Task WhenGitHubFailsMidWay_TheSubmissionFailsInsteadOfStayingStuck()
    {
        _github.GetDefaultBranchAsync(4242, "clinic", "booking").Returns<string>(_ => throw new GitHubRequestFailedException());

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Failed, _submission.Status);
        Assert.Equal("GITHUB_REQUEST_FAILED", _submission.FailureCode);
    }

    [Fact]
    public async Task AnUnexpectedError_FailsTheSubmissionInsteadOfLeavingItAnalyzing()
    {
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>())
            .Returns<AiJsonResponse>(_ => throw new InvalidOperationException("boom"));

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Failed, _submission.Status);
        Assert.Equal("ANALYSIS_ERROR", _submission.FailureCode);
    }

    // ---- AI failures ----

    [Fact]
    public async Task AiRateLimited_FailsWithItsCode()
    {
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>())
            .Returns<AiJsonResponse>(_ => throw new AiRateLimitedException());

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Failed, _submission.Status);
        Assert.Equal("AI_RATE_LIMITED", _submission.FailureCode);
    }

    [Fact]
    public async Task AiAnswerThatBreaksTheRules_FailsAndKeepsTheRawTextForDebugging()
    {
        GivenAiAnswers(0.4m, [Guid.NewGuid().ToString()]);

        await Analyze();

        Assert.Equal("AI_OUTPUT_INVALID", _submission.FailureCode);
        Assert.NotNull(_submission.AiRawResponse);
        Assert.Null(_submission.ConfidenceScore);
    }

    [Fact]
    public async Task AiAnswerThatIsNotJson_Fails()
    {
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AiJsonResponse { Json = "not json", Model = "m" });

        await Analyze();

        Assert.Equal("AI_OUTPUT_INVALID", _submission.FailureCode);
    }

    // ---- the feature changed under the developer ----

    [Fact]
    public async Task AFeatureRemovedFromTheScopeInTheMeantime_FailsWithoutTheAi()
    {
        _mine.Status = SnapshotFeatureStatus.Replaced;

        await Analyze();

        Assert.Equal("FEATURE_NOT_ACTIVE", _submission.FailureCode);
        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }

    // ---- layer 1: skipping the AI ----

    private BuildSubmission GivenAnEarlierVerdict(BuildSubmissionStatus status, int snapshotVersion = 3)
    {
        var earlier = new BuildSubmission
        {
            WorkspaceId = _workspace.Id, FeatureId = _mine.FeatureId, HeadSha = OldHead, SnapshotVersion = snapshotVersion,
            Status = status, ConfidenceScore = 0.5m, ThresholdAtCheck = 0.75m,
            SuspectedFeatureIds = [_other.FeatureId], FindingsJson = "[{\"file\":\"x\",\"description\":\"d\"}]"
        };
        _submissions.GetLatestVerdictAsync(_workspace.Id, _mine.FeatureId, _submission.Id).Returns(earlier);
        return earlier;
    }

    private void GivenNewCommits(string message, params ChangedFile[] files) =>
        _github.CompareAsync(4242, "clinic", "booking", OldHead, Head)
            .Returns(new GitHubComparison([message], files, Truncated: false));

    [Fact]
    public async Task MaintenanceOnlyCommits_SkipTheAiAndRepeatTheEarlierVerdict()
    {
        var earlier = GivenAnEarlierVerdict(BuildSubmissionStatus.Drift);
        GivenNewCommits("refactor: tidy", new ChangedFile("src/a.ts", "modified", "@@ -1 +1 @@\n-// a\n+// b"));

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.SkippedMaintenance, _submission.Status);
        Assert.Equal(0.5m, _submission.ConfidenceScore);
        Assert.Equal([_other.FeatureId], _submission.SuspectedFeatureIds);
        Assert.Equal(earlier.FindingsJson, _submission.FindingsJson);
        Assert.Equal(earlier.Id, _submission.CarriedFromSubmissionId);
        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }

    [Fact]
    public async Task SkippingTheAiForMaintenance_NeitherRaisesNorClosesAnAlert()
    {
        GivenAnEarlierVerdict(BuildSubmissionStatus.Drift);
        var open = new DriftAlert { WorkspaceId = _workspace.Id, FeatureId = _mine.FeatureId };
        _alerts.GetOpenByFeatureAsync(_workspace.Id, _mine.FeatureId).Returns(new List<DriftAlert> { open });
        GivenNewCommits("refactor: tidy", new ChangedFile("src/a.ts", "modified", "@@ -1 +1 @@\n-// a\n+// b"));

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.SkippedMaintenance, _submission.Status);
        Assert.Equal(DriftAlertStatus.Unresolved, open.Status);
        await _alerts.DidNotReceiveWithAnyArgs().AddAsync(default!);
    }

    [Fact]
    public async Task ARefactorMessageThatAddsLogic_StillGoesToTheAi()
    {
        GivenAnEarlierVerdict(BuildSubmissionStatus.InScope);
        GivenNewCommits("refactor: tidy", new ChangedFile("src/a.ts", "modified", "@@ -1 +1,2 @@\n x();\n+chargeCard();"));
        GivenAiAnswers(0.3m);

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.Drift, _submission.Status);
        await _ai.Received(1).GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnEarlierVerdictAgainstAnOlderScope_CannotVouchForTheCode()
    {
        GivenAnEarlierVerdict(BuildSubmissionStatus.Drift, snapshotVersion: 2);
        GivenNewCommits("refactor", new ChangedFile("src/a.ts", "modified", "@@ -1 +1 @@\n-// a\n+// b"));
        GivenAiAnswers(0.9m);

        await Analyze();

        Assert.Equal(BuildSubmissionStatus.InScope, _submission.Status);
        await _ai.Received(1).GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnEarlierCommitGitHubNoLongerKnows_SendsEverythingToTheAi()
    {
        GivenAnEarlierVerdict(BuildSubmissionStatus.InScope);
        _github.CompareAsync(4242, "clinic", "booking", OldHead, Head).Returns((GitHubComparison?)null);
        GivenAiAnswers(0.9m);

        await Analyze();

        await _ai.Received(1).GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstSubmitOfAFeature_IsNeverSkipped()
    {
        _submissions.GetLatestVerdictAsync(default, default, default).ReturnsForAnyArgs((BuildSubmission?)null);
        GivenAiAnswers(0.9m);

        await Analyze();

        await _github.DidNotReceive().CompareAsync(4242, "clinic", "booking", OldHead, Head);
        await _ai.Received(1).GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>());
    }

    // ---- session lock ----

    [Fact]
    public async Task ASubmissionAlreadyReplacedByANewerOne_IsLeftAlone()
    {
        _submission.Status = BuildSubmissionStatus.Superseded;

        await Analyze();

        await _github.DidNotReceiveWithAnyArgs().GetDefaultBranchAsync(default, default!, default!);
        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }

    [Fact]
    public async Task AVerdictThatArrivesAfterANewerSubmit_IsDroppedNotSaved()
    {
        GivenAiAnswers(0.9m);
        _submissions.IsAnalyzingAsync(_submission.Id).Returns(false);

        await Analyze();

        await _submissions.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task WhenStoppedByANewerSubmit_TheCancellationReachesTheCaller()
    {
        using var stop = new CancellationTokenSource();
        _ai.GenerateJsonAsync(Arg.Any<AiJsonRequest>(), Arg.Any<CancellationToken>()).Returns<AiJsonResponse>(_ =>
        {
            stop.Cancel();
            throw new OperationCanceledException(stop.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Analyze(stop.Token));

        await _submissions.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task AnUnknownSubmission_IsIgnored()
    {
        await _service.AnalyzeAsync(Guid.NewGuid(), CancellationToken.None);

        await _ai.DidNotReceiveWithAnyArgs().GenerateJsonAsync(default!, default);
    }
}
