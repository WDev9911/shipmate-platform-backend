namespace ShipMate.Application.DTOs.Workspaces;

public class LinkGitHubRepoRequest
{
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
