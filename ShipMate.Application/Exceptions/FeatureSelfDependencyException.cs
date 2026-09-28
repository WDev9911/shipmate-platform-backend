namespace ShipMate.Application.Exceptions;

public class FeatureSelfDependencyException : AppException
{
    public override string ErrorCode => "FEATURE_SELF_DEPENDENCY";
    public override int StatusCode => 400;

    public FeatureSelfDependencyException()
        : base("A feature can't depend on itself.")
    {
    }
}
