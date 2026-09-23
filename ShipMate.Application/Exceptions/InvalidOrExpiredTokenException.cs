namespace ShipMate.Application.Exceptions;

public class InvalidOrExpiredTokenException : AppException
{
    public override string ErrorCode => "INVALID_OR_EXPIRED_TOKEN";
    public override int StatusCode => 400;

    public InvalidOrExpiredTokenException() : base("The token is invalid or has expired.")
    {
    }
}
