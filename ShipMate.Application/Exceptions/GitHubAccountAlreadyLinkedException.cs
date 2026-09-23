namespace ShipMate.Application.Exceptions;

public class GitHubAccountAlreadyLinkedException : AppException
{
    public override string ErrorCode => "GITHUB_ACCOUNT_ALREADY_LINKED";
    public override int StatusCode => 409;

    public GitHubAccountAlreadyLinkedException()
        : base("This GitHub account is already linked to a different ShipMate account.")
    {
    }
}
