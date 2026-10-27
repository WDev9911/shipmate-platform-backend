using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShipMate.Application.Ai;
using ShipMate.Application.Ai.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class BuildAnalysisService : IBuildAnalysisService
{
    // The most patch text sent to the AI in one go. Beyond it the developer is asked to split the work: cutting the diff
    // would risk hiding the very code that drifts.
    private const int DefaultMaxDiffCharacters = 60_000;

    private readonly IBuildSubmissionRepository _submissions;
    private readonly IDriftAlertRepository _alerts;
    private readonly IWorkspaceRepository _workspaces;
    private readonly IProductDefinitionRepository _definitions;
    private readonly IGitHubAppService _gitHub;
    private readonly IAiGateway _aiGateway;
    private readonly IConfiguration _configuration;
    private readonly IWorkspaceNotifier _notifier;
    private readonly ILogger<BuildAnalysisService> _logger;

    public BuildAnalysisService(
        IBuildSubmissionRepository submissions,
        IDriftAlertRepository alerts,
        IWorkspaceRepository workspaces,
        IProductDefinitionRepository definitions,
        IGitHubAppService gitHub,
        IAiGateway aiGateway,
        IConfiguration configuration,
        IWorkspaceNotifier notifier,
        ILogger<BuildAnalysisService> logger)
    {
        _submissions = submissions;
        _alerts = alerts;
        _workspaces = workspaces;
        _definitions = definitions;
        _gitHub = gitHub;
        _aiGateway = aiGateway;
        _configuration = configuration;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task AnalyzeAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await _submissions.GetByIdAsync(submissionId);
        if (submission is null || submission.Status != BuildSubmissionStatus.Analyzing)
        {
            return;
        }

        try
        {
            await RunAsync(submission, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AppException ex) when (ex is GitHubRequestFailedException or GitHubAppUninstalledException)
        {
            await FinishAsync(submission, BuildSubmissionStatus.Failed, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis of build submission {SubmissionId} failed unexpectedly.", submissionId);
            await FinishAsync(submission, BuildSubmissionStatus.Failed, "ANALYSIS_ERROR");
        }
    }

    private async Task RunAsync(BuildSubmission submission, CancellationToken cancellationToken)
    {
        var workspace = await _workspaces.GetByIdAsync(submission.WorkspaceId);
        if (workspace is null || workspace.GitHubInstallationId is null
            || string.IsNullOrEmpty(workspace.GitHubRepoOwner) || string.IsNullOrEmpty(workspace.GitHubRepoName))
        {
            await FinishAsync(submission, BuildSubmissionStatus.Failed, "WORKSPACE_REPO_NOT_LINKED");
            return;
        }

        // The scope may have changed since the submit (a Feature Challenge), so check against the latest snapshot.
        var snapshot = await _definitions.GetLatestSnapshotAsync(workspace.Id);
        var feature = snapshot?.Features.FirstOrDefault(
            f => f.FeatureId == submission.FeatureId && f.Status == SnapshotFeatureStatus.Locked);
        if (snapshot is null || feature is null)
        {
            await FinishAsync(submission, BuildSubmissionStatus.Failed, "FEATURE_NOT_ACTIVE");
            return;
        }

        submission.SnapshotVersion = snapshot.Version;
        var installationId = workspace.GitHubInstallationId.Value;
        var owner = workspace.GitHubRepoOwner;
        var repo = workspace.GitHubRepoName;

        if (await TrySkipMaintenanceAsync(submission, snapshot.Version, installationId, owner, repo))
        {
            return;
        }

        var defaultBranch = await _gitHub.GetDefaultBranchAsync(installationId, owner, repo);
        var comparison = await _gitHub.CompareAsync(installationId, owner, repo, defaultBranch, submission.HeadSha);
        if (comparison is null)
        {
            await FinishAsync(submission, BuildSubmissionStatus.Failed, "GITHUB_REQUEST_FAILED");
            return;
        }

        var maxDiffCharacters = int.TryParse(_configuration["Build:MaxDiffCharacters"], out var configured)
            ? configured
            : DefaultMaxDiffCharacters;
        if (comparison.Truncated || comparison.Files.Sum(f => f.Patch?.Length ?? 0) > maxDiffCharacters)
        {
            await FinishAsync(submission, BuildSubmissionStatus.Failed, "DIFF_TOO_LARGE");
            return;
        }

        submission.ThresholdAtCheck = workspace.DriftConfidenceThreshold;

        // Nothing differs from main: there is no code to judge.
        if (comparison.Files.Count == 0)
        {
            submission.ConfidenceScore = 1m;
            await FinishAsync(submission, BuildSubmissionStatus.InScope, failureCode: null);
            return;
        }

        var input = BuildInput(snapshot, feature, comparison);

        BuildAnalysisResult result;
        try
        {
            result = await BuildAnalysisClient.AnalyzeAsync(_aiGateway, input, cancellationToken);
        }
        catch (AppException ex) when (ex is AiRateLimitedException or AiProviderUnavailableException or AiOutputInvalidException)
        {
            submission.AiModel = _aiGateway.Model;
            submission.AiRawResponse = (ex as AiOutputInvalidException)?.RawResponse;
            await FinishAsync(submission, BuildSubmissionStatus.Failed, ex.ErrorCode);
            return;
        }

        var output = result.Output;
        submission.AiModel = result.Model;
        submission.AiRawResponse = result.RawJson;
        submission.ConfidenceScore = output.ConfidenceScore;
        submission.SuspectedFeatureIds = output.SuspectedFeatureIds.Select(Guid.Parse).ToList();
        submission.FindingsJson = JsonSerializer.Serialize(output.Findings, AiJson.Options);
        submission.SuggestionsJson = JsonSerializer.Serialize(output.Suggestions, AiJson.Options);

        var status = output.ConfidenceScore < workspace.DriftConfidenceThreshold
            ? BuildSubmissionStatus.Drift
            : BuildSubmissionStatus.InScope;
        await FinishAsync(submission, status, failureCode: null);
    }

    // Layer 1. Only when an earlier verdict of this feature, against the same scope, can stand for the old code.
    private async Task<bool> TrySkipMaintenanceAsync(
        BuildSubmission submission, int snapshotVersion, long installationId, string owner, string repo)
    {
        var baseline = await _submissions.GetLatestVerdictAsync(submission.WorkspaceId, submission.FeatureId, submission.Id);
        if (baseline is null || baseline.SnapshotVersion != snapshotVersion)
        {
            return false;
        }

        GitHubComparison? sinceBaseline;
        try
        {
            sinceBaseline = await _gitHub.CompareAsync(installationId, owner, repo, baseline.HeadSha, submission.HeadSha);
        }
        catch (GitHubRequestFailedException)
        {
            return false;
        }

        // A baseline GitHub no longer knows (force push) can't vouch for anything: the AI looks at everything.
        if (sinceBaseline is null || sinceBaseline.Truncated
            || !MaintenanceChangePolicy.IsMaintenanceOnly(sinceBaseline.CommitMessages, sinceBaseline.Files))
        {
            return false;
        }

        submission.ConfidenceScore = baseline.ConfidenceScore;
        submission.ThresholdAtCheck = baseline.ThresholdAtCheck;
        submission.SuspectedFeatureIds = baseline.SuspectedFeatureIds.ToList();
        submission.FindingsJson = baseline.FindingsJson;
        submission.SuggestionsJson = baseline.SuggestionsJson;
        submission.CarriedFromSubmissionId = baseline.Id;
        await FinishAsync(submission, BuildSubmissionStatus.SkippedMaintenance, failureCode: null);
        return true;
    }

    private static BuildAnalysisInput BuildInput(MvpSnapshot snapshot, SnapshotFeature feature, GitHubComparison comparison) => new()
    {
        Feature = new BuildFeatureInput
        {
            Id = feature.FeatureId.ToString(),
            Name = feature.Name,
            Description = feature.Description,
            Scope = feature.Scope
        },
        OtherFeatures = snapshot.Features
            .Where(f => f.FeatureId != feature.FeatureId)
            .Select(f => new BuildOtherFeatureInput
            {
                Id = f.FeatureId.ToString(),
                Name = f.Name,
                Description = f.Description,
                Status = f.Status.ToString().ToLowerInvariant()
            })
            .ToList(),
        Dependencies = snapshot.Dependencies
            .Select(d => new BuildDependencyInput
            {
                FeatureId = d.FeatureId.ToString(),
                DependsOnFeatureId = d.DependsOnFeatureId.ToString()
            })
            .ToList(),
        Diff = comparison.Files
            .Select(f => new BuildDiffFileInput { File = f.Path, Status = f.Status, Patch = f.Patch })
            .ToList()
    };

    // A newer submit may have replaced this one while it ran. Its verdict is then dropped, not saved over the newer state.
    private async Task FinishAsync(BuildSubmission submission, BuildSubmissionStatus status, string? failureCode)
    {
        if (!await _submissions.IsAnalyzingAsync(submission.Id))
        {
            return;
        }

        submission.Status = status;
        submission.FailureCode = failureCode;
        submission.AnalyzedAt = DateTime.UtcNow;

        var raised = await UpdateAlertsAsync(submission);
        await _submissions.SaveChangesAsync();

        await PublishAsync(submission, raised);
    }

    // The verdict and the alerts it causes are saved together, so a Drift never exists without its alert.
    // SkippedMaintenance repeats an old verdict and never raises or closes an alert. Returns the alert raised, if any.
    private async Task<DriftAlert?> UpdateAlertsAsync(BuildSubmission submission)
    {
        if (submission.Status is not (BuildSubmissionStatus.Drift or BuildSubmissionStatus.InScope))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        foreach (var open in await _alerts.GetOpenByFeatureAsync(submission.WorkspaceId, submission.FeatureId))
        {
            // Out of scope again: the new alert below replaces the old one. In scope: the drift is gone.
            open.Resolve(submission.Id, now);
        }

        if (submission.Status != BuildSubmissionStatus.Drift)
        {
            return null;
        }

        var alert = new DriftAlert
        {
            WorkspaceId = submission.WorkspaceId,
            FeatureId = submission.FeatureId,
            SubmissionId = submission.Id,
            SnapshotVersionAtCheck = submission.SnapshotVersion,
            ThresholdAtCheck = submission.ThresholdAtCheck!.Value,
            ConfidenceScore = submission.ConfidenceScore!.Value,
            SuspectedFeatureIds = submission.SuspectedFeatureIds.ToList()
        };
        await _alerts.AddAsync(alert);
        return alert;
    }

    // Told after the verdict is saved, so what clients hear is already true when they read it over the API.
    private async Task PublishAsync(BuildSubmission submission, DriftAlert? raised)
    {
        await _notifier.PublishAsync(submission.WorkspaceId, WorkspaceEvents.AnalysisCompleted, new
        {
            submissionId = submission.Id,
            featureId = submission.FeatureId,
            status = submission.Status.ToString(),
            failureCode = submission.FailureCode
        });

        if (raised is not null)
        {
            await _notifier.PublishAsync(submission.WorkspaceId, WorkspaceEvents.DriftAlertTriggered, new
            {
                alertId = raised.Id,
                featureId = raised.FeatureId,
                submissionId = raised.SubmissionId,
                confidenceScore = raised.ConfidenceScore,
                thresholdAtCheck = raised.ThresholdAtCheck,
                suspectedFeatureIds = raised.SuspectedFeatureIds
            });
        }

        var suggestions = submission.Status == BuildSubmissionStatus.InScope && submission.SuggestionsJson is not null
            ? JsonSerializer.Deserialize<List<BuildSuggestion>>(submission.SuggestionsJson, AiJson.Options)?.Count ?? 0
            : 0;
        if (suggestions > 0)
        {
            await _notifier.PublishAsync(submission.WorkspaceId, WorkspaceEvents.OptimizationSuggested, new
            {
                submissionId = submission.Id,
                featureId = submission.FeatureId,
                suggestionCount = suggestions
            });
        }
    }
}
