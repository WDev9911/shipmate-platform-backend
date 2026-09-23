namespace ShipMate.Application.Exceptions;

public class WorkspacePermissionDeniedException : AppException
{
    public override string ErrorCode => "WORKSPACE_PERMISSION_DENIED";
    public override int StatusCode => 403;

    public WorkspacePermissionDeniedException()
        : base("Only a Manager of this workspace can perform this action.")
    {
    }
}
