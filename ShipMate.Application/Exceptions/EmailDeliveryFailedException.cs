namespace ShipMate.Application.Exceptions;

public class EmailDeliveryFailedException : AppException
{
    public override string ErrorCode => "EMAIL_DELIVERY_FAILED";
    public override int StatusCode => 502;

    public EmailDeliveryFailedException()
        : base("We couldn't send the verification email. Your account is saved: request a new code to try again.")
    {
    }
}
