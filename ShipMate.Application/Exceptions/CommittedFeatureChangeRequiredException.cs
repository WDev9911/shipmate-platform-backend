namespace ShipMate.Application.Exceptions;

public class CommittedFeatureChangeRequiredException : AppException
{
    public override string ErrorCode => "COMMITTED_FEATURE_CHANGE_REQUIRED";
    public override int StatusCode => 409;

    public CommittedFeatureChangeRequiredException()
        : base("Editing or excluding a feature agreed with the customer requires a change request confirming the customer has been informed.")
    {
    }
}
