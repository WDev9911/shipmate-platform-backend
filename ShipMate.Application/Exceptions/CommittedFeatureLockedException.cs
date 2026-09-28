namespace ShipMate.Application.Exceptions;

public class CommittedFeatureLockedException : AppException
{
    public override string ErrorCode => "COMMITTED_FEATURE_LOCKED";
    public override int StatusCode => 409;

    public CommittedFeatureLockedException()
        : base("Committed features can't be edited or deleted directly after the first AI analysis. Submit a change request instead.")
    {
    }
}
