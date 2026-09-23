namespace ShipMate.Application.DTOs.Workspaces;

public class CreateWorkspaceRequest
{
    public string Name { get; set; } = string.Empty;
    public string VisionPrompt { get; set; } = string.Empty;
    public DateTime? LaunchDeadline { get; set; }
}
