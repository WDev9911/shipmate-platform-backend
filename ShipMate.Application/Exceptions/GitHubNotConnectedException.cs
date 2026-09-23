namespace ShipMate.Application.Exceptions;

public class GitHubNotConnectedException : AppException
{
    public override string ErrorCode => "GITHUB_NOT_CONNECTED";
    public override int StatusCode => 400;

    public GitHubNotConnectedException()
        : base("Connect (or reconnect) your GitHub account before linking a repository to this workspace.")
    {
    }
}
