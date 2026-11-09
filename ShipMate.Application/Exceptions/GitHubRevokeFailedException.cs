namespace ShipMate.Application.Exceptions;

public class GitHubRevokeFailedException : AppException
{
    public override string ErrorCode => "GITHUB_REVOKE_FAILED";
    public override int StatusCode => 502;

    public GitHubRevokeFailedException()
        : base("ShipMate could not take this member's access to the repository away, so nothing was changed. Try again in a moment.")
    {
    }
}
