namespace ShipMate.Application.Exceptions;

public class GitHubRepoEmptyException : AppException
{
    public override string ErrorCode => "GITHUB_REPO_EMPTY";
    public override int StatusCode => 409;

    public GitHubRepoEmptyException()
        : base("This repository has no commits yet, so ShipMate can't protect its main branch. Make a first commit (a README is enough) and link it again.")
    {
    }
}
