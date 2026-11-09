namespace ShipMate.Application.Exceptions;

public class GitHubRequiredToJoinException : AppException
{
    public override string ErrorCode => "GITHUB_REQUIRED_TO_JOIN";
    public override int StatusCode => 400;

    public GitHubRequiredToJoinException()
        : base("Connect your GitHub account before accepting this invitation: workspace members get access to the repository.")
    {
    }
}
