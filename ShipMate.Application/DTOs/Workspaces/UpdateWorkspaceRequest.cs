namespace ShipMate.Application.DTOs.Workspaces;

public class UpdateWorkspaceRequest
{
    // True partial update: a field left null means "don't change it", not "clear it".
    // (This means an already-set LaunchDeadline currently can't be cleared back to null
    // through this endpoint — only replaced with a different date.)
    public string? Name { get; set; }
    public string? VisionPrompt { get; set; }
    public DateTime? LaunchDeadline { get; set; }
}
