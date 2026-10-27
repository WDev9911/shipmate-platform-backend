namespace ShipMate.Domain.Common;

/// <summary>The kinds of event written to the audit trail. SHIP reads these names, so they don't change.</summary>
public static class AuditTypes
{
    // LOCK
    public const string SnapshotLocked = "SNAPSHOT_LOCKED";
    public const string ScopeChange = "SCOPE_CHANGE";

    // BUILD
    public const string ScopeBypass = "SCOPE_BYPASS";
    public const string SoloOverrideLogged = "SOLO_OVERRIDE_LOGGED";
    public const string SystemAlert = "SYSTEM_ALERT";
    public const string DirectPushDetected = "DIRECT_PUSH_DETECTED";
    public const string ThresholdChanged = "THRESHOLD_CHANGED";
    public const string GovernanceDilution = "GOVERNANCE_DILUTION";
}
