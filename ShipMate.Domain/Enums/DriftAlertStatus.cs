namespace ShipMate.Domain.Enums;

public enum DriftAlertStatus
{
    // Raised by a submission that went beyond the locked scope; nobody has reacted yet.
    Unresolved,
    // The developer holding the feature saw it. Still blocks approval.
    Acknowledged,
    // A Manager accepted the drift on purpose, with a reason on record.
    Overridden,
    // The drift is gone: a later submission is in scope, or a newer alert took this one's place.
    Resolved
}
