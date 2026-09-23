namespace ShipMate.Application.Exceptions;

public class WorkspaceMustHaveManagerException : AppException
{
    public override string ErrorCode => "WORKSPACE_MUST_HAVE_MANAGER";
    public override int StatusCode => 400;

    public WorkspaceMustHaveManagerException()
        : base("A workspace must always have exactly one Manager. Promote another member to Manager instead.")
    {
    }
}
