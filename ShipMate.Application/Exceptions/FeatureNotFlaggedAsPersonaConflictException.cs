namespace ShipMate.Application.Exceptions;

public class FeatureNotFlaggedAsPersonaConflictException : AppException
{
    public override string ErrorCode => "FEATURE_NOT_FLAGGED_AS_PERSONA_CONFLICT";
    public override int StatusCode => 409;

    public FeatureNotFlaggedAsPersonaConflictException()
        : base("This feature is not flagged as conflicting with the persona.")
    {
    }
}
