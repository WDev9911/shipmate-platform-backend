namespace ShipMate.Domain.Enums;

public enum ShipAttemptStatus
{
    // The production URL didn't pass every check; nothing was shipped. A new attempt is needed.
    ChecksFailed,
    // Every check passed and the attempt minted the Ship Card of its version.
    Shipped
}
