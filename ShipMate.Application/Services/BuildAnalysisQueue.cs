using System.Collections.Concurrent;
using System.Threading.Channels;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Services;

public record QueuedAnalysis(Guid SubmissionId, Guid FeatureId);

/// <summary>
/// In-memory work queue for submissions. Different features are analysed side by side; a new submit of a feature
/// cancels the analysis that is still running for it, so only the latest commit ends up analysed.
/// </summary>
public class BuildAnalysisQueue : IBuildAnalysisQueue
{
    private readonly Channel<QueuedAnalysis> _channel = Channel.CreateUnbounded<QueuedAnalysis>();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public ChannelReader<QueuedAnalysis> Reader => _channel.Reader;

    public void Enqueue(Guid submissionId, Guid featureId)
    {
        if (_running.TryGetValue(featureId, out var running))
        {
            try
            {
                running.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // It just finished on its own.
            }
        }

        _channel.Writer.TryWrite(new QueuedAnalysis(submissionId, featureId));
    }

    public CancellationTokenSource BeginRun(Guid featureId, CancellationToken stoppingToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _running[featureId] = source;
        return source;
    }

    public void EndRun(Guid featureId, CancellationTokenSource source)
    {
        // Only forget our own run: a newer one may already have taken the slot.
        _running.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(featureId, source));
        source.Dispose();
    }
}
