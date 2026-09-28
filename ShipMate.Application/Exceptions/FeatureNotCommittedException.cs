namespace ShipMate.Application.Exceptions;

public class FeatureNotCommittedException : AppException
{
    public override string ErrorCode => "FEATURE_NOT_COMMITTED";
    public override int StatusCode => 409;

    public FeatureNotCommittedException()
        : base("Only features agreed with the customer need a change request. Edit or exclude this feature directly.")
    {
    }
}
