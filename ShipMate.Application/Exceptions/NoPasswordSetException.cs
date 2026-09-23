namespace ShipMate.Application.Exceptions;

public class NoPasswordSetException : AppException
{
    public override string ErrorCode => "NO_PASSWORD_SET";
    public override int StatusCode => 400;

    public NoPasswordSetException()
        : base("This account has no password set (GitHub-only). Sign in with GitHub instead.")
    {
    }
}
