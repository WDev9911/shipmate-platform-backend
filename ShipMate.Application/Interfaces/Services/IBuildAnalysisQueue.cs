namespace ShipMate.Application.Interfaces.Services;

public interface IBuildAnalysisQueue
{
    /// <summary>Queues the analysis of a submission, and stops the analysis still running for an older submit of the same feature.</summary>
    void Enqueue(Guid submissionId, Guid featureId);
}
