using ShipMate.Application.DTOs.Build;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>BUILD: the one thing the developer should do next, worked out by rules from the scope, the assignments and the alerts.</summary>
public interface INextBestActionService
{
    /// <summary>Any active member may ask; the answer is about the caller. Never calls the AI.</summary>
    Task<NextBestActionDto> GetAsync(Guid userId, Guid workspaceId);
}
