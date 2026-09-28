namespace ShipMate.Application.Exceptions;

public class DependencyAlreadyExistsException : AppException
{
    public override string ErrorCode => "DEPENDENCY_ALREADY_EXISTS";
    public override int StatusCode => 409;

    public DependencyAlreadyExistsException()
        : base("This feature already depends on that feature.")
    {
    }
}
