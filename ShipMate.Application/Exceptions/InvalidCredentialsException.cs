namespace ShipMate.Application.Exceptions;

public class InvalidCredentialsException : AppException
{
    public override string ErrorCode => "INVALID_CREDENTIALS";
    public override int StatusCode => 401;

    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }
}
