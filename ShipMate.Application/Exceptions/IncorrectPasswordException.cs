namespace ShipMate.Application.Exceptions;

public class IncorrectPasswordException : AppException
{
    public override string ErrorCode => "INCORRECT_PASSWORD";
    public override int StatusCode => 401;

    public IncorrectPasswordException() : base("The current password is incorrect.")
    {
    }
}
