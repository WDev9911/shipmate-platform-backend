using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Services;

namespace ShipMate.Infrastructure.Services;

/// <summary>Runs the queued submission analyses in the background, each in its own scope.</summary>
public class BuildAnalysisWorker : BackgroundService
{
    private readonly BuildAnalysisQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BuildAnalysisWorker> _logger;

    public BuildAnalysisWorker(
        BuildAnalysisQueue queue, IServiceScopeFactory scopeFactory, ILogger<BuildAnalysisWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueInterruptedAsync();

        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _ = Task.Run(() => AnalyzeAsync(item, stoppingToken), stoppingToken);
        }
    }

    // Analyses cut short by a restart are still marked as analyzing in the database: pick them up again.
    private async Task RequeueInterruptedAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBuildSubmissionRepository>();
            foreach (var submission in await repository.GetAllAnalyzingAsync())
            {
                _queue.Enqueue(submission.Id, submission.FeatureId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Couldn't requeue the build analyses interrupted by the last shutdown.");
        }
    }

    private async Task AnalyzeAsync(QueuedAnalysis item, CancellationToken stoppingToken)
    {
        var run = _queue.BeginRun(item.FeatureId, stoppingToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IBuildAnalysisService>();
            await service.AnalyzeAsync(item.SubmissionId, run.Token);
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer submit, or the app is shutting down: nothing to record.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis of build submission {SubmissionId} crashed.", item.SubmissionId);
        }
        finally
        {
            _queue.EndRun(item.FeatureId, run);
        }
    }
}
