namespace ShipMate.Application.Interfaces.Services;

/// <summary>Real-time messages to the people of a workspace. A message that can't be delivered never fails the work that caused it.</summary>
public interface IWorkspaceNotifier
{
    /// <summary>To everyone connected to the workspace.</summary>
    Task PublishAsync(Guid workspaceId, string eventName, object payload);

    /// <summary>To specific people, on every connection they have open, whichever workspace they are looking at.</summary>
    Task PublishToUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload);
}

/// <summary>The names of the events clients listen for. Fixed by the spec except where noted.</summary>
public static class WorkspaceEvents
{
    // BUILD (named by the spec)
    public const string BranchPushSuccess = "BranchPushSuccess";
    public const string DriftAlertTriggered = "DriftAlertTriggered";
    public const string DriftAlertAcknowledged = "DriftAlertAcknowledged";
    public const string OptimizationSuggested = "OptimizationSuggested";
    public const string CodeScopeApproved = "CodeScopeApproved";
    public const string ManagerOverrideExecuted = "ManagerOverrideExecuted";

    // BUILD (added): a submission got its verdict, and someone blocked on a backup feature can move on
    public const string AnalysisCompleted = "AnalysisCompleted";
    public const string BackupFeatureClaimed = "BackupFeatureClaimed";

    // LOCK (added): the two notifications LOCK owed
    public const string FeatureRemoved = "FeatureRemoved";
    public const string BackupFeatureMatched = "BackupFeatureMatched";
}
