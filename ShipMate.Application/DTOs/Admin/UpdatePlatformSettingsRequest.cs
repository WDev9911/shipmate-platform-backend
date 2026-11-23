namespace ShipMate.Application.DTOs.Admin;

/// <summary>Every field, with the revision it is based on.</summary>
public class UpdatePlatformSettingsRequest
{
    public int ComplexityBudgetPerDeveloper { get; set; }
    public decimal DefaultDriftThreshold { get; set; }
    public List<string> MaintenanceCommitPatterns { get; set; } = [];
    public int Revision { get; set; }
}
