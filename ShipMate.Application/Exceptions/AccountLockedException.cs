namespace ShipMate.Application.Exceptions;

public class AccountLockedException : AppException
{
    public override string ErrorCode => "ACCOUNT_LOCKED";
    public override int StatusCode => 403;

    public AccountLockedException() : base("This account is locked or has been removed.")
    {
    }
}
