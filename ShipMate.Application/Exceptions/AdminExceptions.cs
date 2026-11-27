namespace ShipMate.Application.Exceptions;

public class AdminAccountProtectedException : AppException
{
    public override string ErrorCode => "ADMIN_ACCOUNT_PROTECTED";
    public override int StatusCode => 403;

    public AdminAccountProtectedException() : base("An administrator account cannot be locked or removed from here.")
    {
    }
}

public class NoChangeException : AppException
{
    public override string ErrorCode => "NO_CHANGE";
    public override int StatusCode => 400;

    public NoChangeException() : base("Nothing would change.")
    {
    }
}

public class AccountRemovedFinalException : AppException
{
    public override string ErrorCode => "ACCOUNT_REMOVED_FINAL";
    public override int StatusCode => 409;

    public AccountRemovedFinalException() : base("A removed account cannot change status again.")
    {
    }
}

public class AdminCannotUseWorkspacesException : AppException
{
    public override string ErrorCode => "ADMIN_CANNOT_USE_WORKSPACES";
    public override int StatusCode => 403;

    public AdminCannotUseWorkspacesException() : base("An administrator account manages the platform and cannot create or join workspaces.")
    {
    }
}

public class SettingsChangedByOtherException : AppException
{
    public override string ErrorCode => "SETTINGS_CHANGED_BY_OTHER";
    public override int StatusCode => 409;

    public SettingsChangedByOtherException() : base("The settings were changed by someone else. Load them again and retry.")
    {
    }
}
