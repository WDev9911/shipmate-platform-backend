namespace ShipMate.Application.Exceptions;

public class GitHubAppNotInstalledException : AppException
{
    private readonly string _installUrl;

    public override string ErrorCode => "GITHUB_APP_NOT_INSTALLED";
    public override int StatusCode => 409;
    public override object Details => new { InstallUrl = _installUrl };

    public GitHubAppNotInstalledException(string installUrl)
        : base("Install the ShipMate GitHub App on this repository, then link it again.")
    {
        _installUrl = installUrl;
    }
}
