namespace ShipMate.Application.Exceptions;

public class GitHubRepoNotAccessibleException : AppException
{
    public override string ErrorCode => "GITHUB_REPO_NOT_ACCESSIBLE";
    public override int StatusCode => 403;

    public GitHubRepoNotAccessibleException()
        : base("This GitHub repository doesn't exist or you don't have access to it.")
    {
    }
}
