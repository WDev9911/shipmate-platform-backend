namespace ShipMate.Application.Interfaces.Services;

public interface IBuildAnalysisService
{
    /// <summary>
    /// Gives a submission its verdict: skips the AI for maintenance-only changes, otherwise has the AI compare the
    /// code with the locked scope. Does nothing when the submission was already replaced by a newer one. Throws
    /// OperationCanceledException when stopped because a newer submit arrived.
    /// </summary>
    Task AnalyzeAsync(Guid submissionId, CancellationToken cancellationToken);
}
