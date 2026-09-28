namespace ShipMate.Application.Exceptions;

public class DependencyOnExcludedFeatureException : AppException
{
    public override string ErrorCode => "DEPENDENCY_ON_EXCLUDED_FEATURE";
    public override int StatusCode => 409;

    public DependencyOnExcludedFeatureException()
        : base("An included feature can't depend on an excluded one. Include the prerequisite feature first.")
    {
    }
}
