namespace ShipMate.Application.Exceptions;

public class GitHubEmailUnavailableException : AppException
{
    public override string ErrorCode => "GITHUB_EMAIL_UNAVAILABLE";
    public override int StatusCode => 400;

    public GitHubEmailUnavailableException()
        : base("Your GitHub account has no public, verified email address. Please verify an email on GitHub and try again.")
    {
    }
}
