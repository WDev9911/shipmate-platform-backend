namespace ShipMate.Application.DTOs.Workspaces;

public class GitHubRepoDto
{
    public string Owner { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPrivate { get; set; }
    public string DefaultBranch { get; set; } = string.Empty;
}
