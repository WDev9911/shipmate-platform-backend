namespace ShipMate.Application.Exceptions;

public class WorkspaceMemberAlreadyExistsException : AppException
{
    public override string ErrorCode => "WORKSPACE_MEMBER_ALREADY_EXISTS";
    public override int StatusCode => 409;

    public WorkspaceMemberAlreadyExistsException()
        : base("This user is already a member of the workspace.")
    {
    }
}
