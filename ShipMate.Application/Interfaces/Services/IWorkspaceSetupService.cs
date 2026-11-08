using ShipMate.Application.DTOs.Workspaces;

namespace ShipMate.Application.Interfaces.Services;

public interface IWorkspaceSetupService
{
    /// <summary>
    /// Shows the Manager what still stands between this workspace and a working BUILD: repo, ShipMate App, merge gate,
    /// governance, and which active members have not declared a skill or connected GitHub. Reads stored state only;
    /// it does not call GitHub.
    /// </summary>
    Task<WorkspaceSetupDto> GetAsync(Guid userId, Guid workspaceId);
}
