namespace ShipMate.Domain.Enums;

public enum ChallengeStatus
{
    Open,
    Approved,
    Rejected
}

/// <summary>The three outcomes of a Feature Challenge (LOCK, after the first Lock).</summary>
public enum ChallengeAction
{
    AddDirectly,
    RemoveAndReplace,
    Backup
}

/// <summary>What to do with the features that depend on a feature being removed and replaced.</summary>
public enum RemovalDependentsResolution
{
    RemoveDependents,
    Restructure
}
