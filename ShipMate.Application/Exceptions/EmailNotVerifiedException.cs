namespace ShipMate.Application.Exceptions;

public class EmailNotVerifiedException : AppException
{
    public override string ErrorCode => "EMAIL_NOT_VERIFIED";
    public override int StatusCode => 403;

    public EmailNotVerifiedException() : base("Please verify your email before logging in.")
    {
    }
}
