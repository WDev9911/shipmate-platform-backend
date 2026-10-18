namespace ShipMate.Application.Exceptions;

public class GitHubBranchCreationFailedException : AppException
{
    public override string ErrorCode => "GITHUB_BRANCH_CREATION_FAILED";
    public override int StatusCode => 502;

    public GitHubBranchCreationFailedException()
        : base("GitHub did not create the branch. Check your access to the repository and try again.")
    {
    }
}

public class WorkspaceRepoNotLinkedException : AppException
{
    public override string ErrorCode => "WORKSPACE_REPO_NOT_LINKED";
    public override int StatusCode => 409;

    public WorkspaceRepoNotLinkedException()
        : base("Link a GitHub repository to this workspace first.")
    {
    }
}
