namespace ShipMate.Application.Exceptions;

public class FeatureAlreadyExcludedException : AppException
{
    public override string ErrorCode => "FEATURE_ALREADY_EXCLUDED";
    public override int StatusCode => 409;

    public FeatureAlreadyExcludedException()
        : base("This feature is already excluded.")
    {
    }
}
