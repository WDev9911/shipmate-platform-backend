namespace ShipMate.Domain.Enums;

public enum SnapshotFeatureStatus
{
    Locked,
    // Added by a Feature Challenge but too hard for every current developer; becomes Locked when one takes it.
    Backup,
    // Removed by a Feature Challenge; kept in the snapshot for history, no longer counted.
    Replaced
}
