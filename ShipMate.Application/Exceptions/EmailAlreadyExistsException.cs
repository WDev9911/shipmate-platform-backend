namespace ShipMate.Application.Exceptions;

public class EmailAlreadyExistsException : AppException
{
    public override string ErrorCode => "EMAIL_ALREADY_EXISTS";
    public override int StatusCode => 409;

    public EmailAlreadyExistsException() : base("This email is already registered.")
    {
    }
}
