namespace ShipMate.Application.DTOs.Build;

public class ApproveBuildRequest
{
    // Approve only up to this commit of the submission instead of its head. Optional.
    public string? CommitSha { get; set; }
}
