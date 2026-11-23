namespace ShipMate.Domain.Entities;

/// <summary>
/// The values an administrator can change for the whole platform. There is exactly one row (Id = 1).
/// A workspace gets its own copy of the budget and the drift threshold when it is created, so changing them here never
/// reaches a workspace that already exists.
/// </summary>
public class PlatformSettings
{
    public const int SingleRowId = 1;
    public const int InitialComplexityBudgetPerDeveloper = 10;
    public const decimal InitialDriftThreshold = 0.75m;

    public int Id { get; set; } = SingleRowId;

    /// <summary>The Total Complexity Budget of a new workspace is this many points per active developer.</summary>
    public int ComplexityBudgetPerDeveloper { get; set; } = InitialComplexityBudgetPerDeveloper;

    /// <summary>The drift threshold a new workspace starts with.</summary>
    public decimal DefaultDriftThreshold { get; set; } = InitialDriftThreshold;

    /// <summary>Words that make a commit message count as maintenance; stored trimmed and in lower case.</summary>
    public List<string> MaintenanceCommitPatterns { get; set; } = [];

    /// <summary>Goes up by one on every save, so two administrators can't silently overwrite each other.</summary>
    public int Revision { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
