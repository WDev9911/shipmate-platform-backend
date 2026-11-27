namespace ShipMate.Application.DTOs.Admin;

/// <summary>Counts only: how the platform is doing, without any workspace's content.</summary>
public class PlatformOverviewDto
{
    public DateTime GeneratedAt { get; set; }

    public Dictionary<string, int> UsersByRole { get; set; } = [];
    public Dictionary<string, int> UsersByStatus { get; set; } = [];
    public int NewUsersLast30Days { get; set; }

    public Dictionary<string, int> WorkspacesByStatus { get; set; } = [];

    public Dictionary<string, int> SubmissionsByStatus { get; set; } = [];

    /// <summary>Submissions still Analyzing more than 10 minutes after they were made.</summary>
    public int SubmissionsStuckAnalyzing { get; set; }

    public Dictionary<string, int> DriftAlertsByStatus { get; set; } = [];

    /// <summary>Records of the governance events: dilution, bypass, solo override, direct push, system alert.</summary>
    public Dictionary<string, int> AuditRecordsByType { get; set; } = [];
}
