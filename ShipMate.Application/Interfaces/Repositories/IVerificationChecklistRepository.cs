using ShipMate.Domain.Entities;

namespace ShipMate.Application.Interfaces.Repositories;

public interface IVerificationChecklistRepository
{
    // Saved with whatever else is tracked in the request, so a snapshot and its list are written together.
    Task AddAsync(VerificationChecklist checklist);

    /// <summary>The list of one version, with its items and the features of its snapshot. Tracked, so it can be edited.</summary>
    Task<VerificationChecklist?> GetByVersionAsync(Guid workspaceId, int snapshotVersion);

    /// <summary>The workspace's lists, newest version first, with their items.</summary>
    Task<List<VerificationChecklist>> GetByWorkspaceIdAsync(Guid workspaceId);

    Task<bool> SaveChangesAsync();
}
