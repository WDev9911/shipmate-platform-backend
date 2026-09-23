namespace ShipMate.Application.Exceptions;

public class GitHubDisconnectBlockedByLinkedWorkspacesException : AppException
{
    public override string ErrorCode => "GITHUB_DISCONNECT_BLOCKED_BY_LINKED_WORKSPACES";
    public override int StatusCode => 409;

    public GitHubDisconnectBlockedByLinkedWorkspacesException()
        : base("Unlink GitHub repositories from your workspaces before disconnecting your GitHub account.")
    {
    }
}
