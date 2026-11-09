namespace ShipMate.Application.DTOs.Build;

public class SubmitBuildRequest
{
    // The branch the developer works on, feature/{featureId}-{username}.
    public string BranchName { get; set; } = string.Empty;

    // The commit the CLI just pushed to the staging branch.
    public string CommitSha { get; set; } = string.Empty;
}
