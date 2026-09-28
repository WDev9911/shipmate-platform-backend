namespace ShipMate.Application.Exceptions;

public class FeatureNotFlaggedAsDuplicateException : AppException
{
    public override string ErrorCode => "FEATURE_NOT_FLAGGED_AS_DUPLICATE";
    public override int StatusCode => 409;

    public FeatureNotFlaggedAsDuplicateException()
        : base("This feature is not flagged as a possible duplicate.")
    {
    }
}
