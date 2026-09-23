namespace ShipMate.Application.Exceptions;

public class EmailNotVerifiedCannotLinkException : AppException
{
    public override string ErrorCode => "EMAIL_NOT_VERIFIED_CANNOT_LINK";
    public override int StatusCode => 403;

    public EmailNotVerifiedCannotLinkException()
        : base("An account with this email already exists but is not verified. Please verify it via email/password login first.")
    {
    }
}
