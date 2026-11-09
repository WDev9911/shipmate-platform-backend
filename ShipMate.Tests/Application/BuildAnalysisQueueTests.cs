using ShipMate.Application.Services;

namespace ShipMate.Tests.Application;

public class BuildAnalysisQueueTests
{
    private readonly BuildAnalysisQueue _queue = new();
    private readonly Guid _feature = Guid.NewGuid();

    [Fact]
    public async Task Enqueued_SubmissionsComeOutInOrder()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        _queue.Enqueue(first, _feature);
        _queue.Enqueue(second, Guid.NewGuid());

        Assert.Equal(first, (await _queue.Reader.ReadAsync()).SubmissionId);
        Assert.Equal(second, (await _queue.Reader.ReadAsync()).SubmissionId);
    }

    [Fact]
    public void ANewSubmitOfTheSameFeature_StopsTheRunningAnalysis()
    {
        var running = _queue.BeginRun(_feature, CancellationToken.None);

        _queue.Enqueue(Guid.NewGuid(), _feature);

        Assert.True(running.IsCancellationRequested);
    }

    [Fact]
    public void ASubmitOfAnotherFeature_LeavesTheRunningAnalysisAlone()
    {
        var running = _queue.BeginRun(_feature, CancellationToken.None);

        _queue.Enqueue(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(running.IsCancellationRequested);
    }

    [Fact]
    public void AFinishedRun_IsNotCancelledByLaterSubmits()
    {
        var running = _queue.BeginRun(_feature, CancellationToken.None);
        _queue.EndRun(_feature, running);

        var error = Record.Exception(() => _queue.Enqueue(Guid.NewGuid(), _feature));

        Assert.Null(error);
    }

    [Fact]
    public void AnOlderRunEnding_DoesNotForgetTheNewerRun()
    {
        var older = _queue.BeginRun(_feature, CancellationToken.None);
        var newer = _queue.BeginRun(_feature, CancellationToken.None);

        _queue.EndRun(_feature, older);
        _queue.Enqueue(Guid.NewGuid(), _feature);

        Assert.True(newer.IsCancellationRequested);
    }

    [Fact]
    public void ShuttingDown_StopsTheRunningAnalysis()
    {
        using var shutdown = new CancellationTokenSource();
        var running = _queue.BeginRun(_feature, shutdown.Token);

        shutdown.Cancel();

        Assert.True(running.IsCancellationRequested);
    }
}
