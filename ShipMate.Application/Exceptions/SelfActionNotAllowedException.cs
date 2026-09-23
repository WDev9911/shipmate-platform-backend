namespace ShipMate.Application.Exceptions;

public class SelfActionNotAllowedException : AppException
{
    public override string ErrorCode => "SELF_ACTION_NOT_ALLOWED";
    public override int StatusCode => 400;

    public SelfActionNotAllowedException() : base("You cannot perform this action on your own account.")
    {
    }
}
