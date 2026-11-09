namespace ShipMate.Application.Exceptions;

public class GitHubDisconnectBlockedByLinkedWorkspacesException : AppException
{
    public override string ErrorCode => "GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES";
    public override int StatusCode => 409;

    public GitHubDisconnectBlockedByLinkedWorkspacesException()
        : base("You are a member of a workspace, and members get access to its repository through their GitHub account. Leave the workspace before disconnecting it.")
    {
    }
}
