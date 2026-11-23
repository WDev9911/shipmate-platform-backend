namespace ShipMate.Application.DTOs.Admin;

public class PlatformSettingsDto
{
    public int ComplexityBudgetPerDeveloper { get; set; }
    public decimal DefaultDriftThreshold { get; set; }
    public List<string> MaintenanceCommitPatterns { get; set; } = [];

    /// <summary>Send it back unchanged with the next update; if someone else saved in between, the update is refused.</summary>
    public int Revision { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public PlatformSettingsLimitsDto Limits { get; set; } = new();
}

/// <summary>The bounds of the fields above, and the drift rules a Manager lives by. Read only: an administrator sees them, not changes them.</summary>
public class PlatformSettingsLimitsDto
{
    public int MinBudgetPerDeveloper { get; set; }
    public int MaxBudgetPerDeveloper { get; set; }
    public int MinPatterns { get; set; }
    public int MaxPatterns { get; set; }
    public int MinPatternLength { get; set; }
    public int MaxPatternLength { get; set; }

    /// <summary>The system never lets a threshold go below this.</summary>
    public decimal DriftThresholdHardFloor { get; set; }

    /// <summary>The range of the default threshold, and of what a Manager may set.</summary>
    public decimal DriftThresholdMin { get; set; }
    public decimal DriftThresholdMax { get; set; }

    public int ManagerMaxThresholdChanges { get; set; }
    public int ManagerThresholdCooldownDays { get; set; }
}
