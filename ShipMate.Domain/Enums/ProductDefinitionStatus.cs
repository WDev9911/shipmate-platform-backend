namespace ShipMate.Domain.Enums;

public enum ProductDefinitionStatus
{
    Draft,
    ReadyForLock,
    // Set by LOCK on the first successful Lock; DEFINE is frozen from then on (DEFINE step 9).
    Locked
}
