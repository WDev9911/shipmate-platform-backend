namespace ShipMate.Application.Exceptions;

public class GitHubDisconnectRequiresPasswordException : AppException
{
    public override string ErrorCode => "GITHUB_DISCONNECT_REQUIRES_PASSWORD";
    public override int StatusCode => 400;

    public GitHubDisconnectRequiresPasswordException()
        : base("Set a password before disconnecting GitHub, otherwise you'd be locked out of your account.")
    {
    }
}
