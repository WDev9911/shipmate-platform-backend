namespace ShipMate.Application.Exceptions;

public class GovernanceSuspendedException : AppException
{
    public override string ErrorCode => "GOVERNANCE_SUSPENDED";
    public override int StatusCode => 409;

    public GovernanceSuspendedException()
        : base("ShipMate lost access to this repository. Install the ShipMate GitHub App again to continue.")
    {
    }
}

public class ProtectedBranchSubmitException : AppException
{
    public override string ErrorCode => "PROTECTED_BRANCH_SUBMIT";
    public override int StatusCode => 400;

    public ProtectedBranchSubmitException()
        : base("Code can't be submitted from main, develop or release branches. Use a hotfix branch for urgent fixes.")
    {
    }
}

public class HotfixNotSupportedException : AppException
{
    public override string ErrorCode => "HOTFIX_NOT_SUPPORTED";
    public override int StatusCode => 400;

    public HotfixNotSupportedException()
        : base("Hotfix submissions are not supported yet.")
    {
    }
}

public class InvalidFeatureIdException : AppException
{
    public override string ErrorCode => "INVALID_FEATURE_ID";
    public override int StatusCode => 400;

    public InvalidFeatureIdException()
        : base("The branch name doesn't point to a feature you hold. Pull the branch ShipMate created for you and try again.")
    {
    }
}

public class FeatureReplacedException : AppException
{
    public override string ErrorCode => "FEATURE_REPLACED";
    public override int StatusCode => 409;

    public FeatureReplacedException()
        : base("This feature was removed from the MVP scope.")
    {
    }
}

public class FeatureIsBackupException : AppException
{
    public override string ErrorCode => "FEATURE_IS_BACKUP";
    public override int StatusCode => 409;

    public FeatureIsBackupException()
        : base("This feature is a backup: it can be built once a developer with enough skill takes it.")
    {
    }
}

public class FeatureNotAssignedException : AppException
{
    public override string ErrorCode => "FEATURE_NOT_ASSIGNED";
    public override int StatusCode => 409;

    public FeatureNotAssignedException()
        : base("Nobody holds this feature yet. Take it in Feature Assignment first.")
    {
    }
}

public class FeatureAssignedToOtherException : AppException
{
    public override string ErrorCode => "FEATURE_ASSIGNED_TO_OTHER";
    public override int StatusCode => 403;

    public FeatureAssignedToOtherException()
        : base("This branch belongs to another developer.")
    {
    }
}

public class CommitNotInStagingException : AppException
{
    public override string ErrorCode => "COMMIT_NOT_IN_STAGING";
    public override int StatusCode => 400;

    public CommitNotInStagingException()
        : base("That commit is not the head of your staging branch. Push to staging and submit again.")
    {
    }
}

/// <summary>GitHub no longer knows the App installation: someone uninstalled the App.</summary>
public class GitHubAppUninstalledException : AppException
{
    public override string ErrorCode => "GITHUB_APP_UNINSTALLED";
    public override int StatusCode => 409;

    public GitHubAppUninstalledException()
        : base("The ShipMate GitHub App is no longer installed on this repository.")
    {
    }
}

public class GitHubRequestFailedException : AppException
{
    public override string ErrorCode => "GITHUB_REQUEST_FAILED";
    public override int StatusCode => 502;

    public GitHubRequestFailedException()
        : base("GitHub didn't answer as expected. Try again in a moment.")
    {
    }
}

public class DriftAlertNotOpenException : AppException
{
    public override string ErrorCode => "INVALID_ALERT_TRANSITION";
    public override int StatusCode => 409;

    public DriftAlertNotOpenException()
        : base("This alert can't move to that state from where it is now.")
    {
    }
}

public class NotFeatureHolderException : AppException
{
    public override string ErrorCode => "NOT_FEATURE_HOLDER";
    public override int StatusCode => 403;

    public NotFeatureHolderException()
        : base("Only the developer holding this feature can do this.")
    {
    }
}

public class SubmissionNotReadyException : AppException
{
    public override string ErrorCode => "SUBMISSION_NOT_READY";
    public override int StatusCode => 409;

    public SubmissionNotReadyException()
        : base("This submission isn't waiting for approval: it is still being analysed, failed, or already approved.")
    {
    }
}

public class SubmissionSupersededException : AppException
{
    public override string ErrorCode => "SUBMISSION_SUPERSEDED";
    public override int StatusCode => 409;

    public SubmissionSupersededException()
        : base("A newer submission of this feature replaced this one. Approve the newest.")
    {
    }
}

public class DriftUnresolvedException : AppException
{
    public override string ErrorCode => "DRIFT_UNRESOLVED";
    public override int StatusCode => 409;

    public DriftUnresolvedException()
        : base("The code goes beyond the locked scope. Fix it and submit again, send a Feature Challenge, or ask a Manager to override.")
    {
    }
}

public class FastForwardFailedException : AppException
{
    public override string ErrorCode => "FAST_FORWARD_FAILED";
    public override int StatusCode => 409;

    public FastForwardFailedException()
        : base("The feature branch can't move to this commit because the histories diverged. Rebase on the feature branch and submit again.")
    {
    }
}

public class CommitNotInSubmissionException : AppException
{
    public override string ErrorCode => "COMMIT_NOT_IN_SUBMISSION";
    public override int StatusCode => 400;

    public CommitNotInSubmissionException()
        : base("That commit isn't among the commits of this submission that the feature branch doesn't have yet.")
    {
    }
}
