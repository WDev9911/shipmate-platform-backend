using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// BUILD: finds code that reached the repository without going through ShipMate. On repositories where GitHub can't
/// stop that (personal ones) this is the safety net: every find is written to the audit trail.
/// </summary>
public interface IDirectPushDetector
{
    /// <summary>
    /// Checks the developer's feature branch and the default branch. Changes the workspace and assignment bookmarks and
    /// appends audit records without saving; the caller saves them with its own change. Never throws for GitHub
    /// trouble: a check that can't run now simply runs again at the next submit.
    /// </summary>
    Task DetectAsync(Workspace workspace, FeatureAssignment assignment);
}
