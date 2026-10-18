namespace ShipMate.Application.Exceptions;

/// <summary>Two Locks or two Feature Challenge approvals raced for the same snapshot version.</summary>
public class SnapshotVersionConflictException : AppException
{
    public override string ErrorCode => "SNAPSHOT_VERSION_CONFLICT";
    public override int StatusCode => 409;

    public SnapshotVersionConflictException()
        : base("The MVP snapshot was just changed by someone else. Reload and try again.")
    {
    }
}
