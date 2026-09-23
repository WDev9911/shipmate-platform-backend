namespace ShipMate.Application.DTOs.Auth;

public class GitHubProfileResult
{
    public string GitHubId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Name { get; set; }
}
