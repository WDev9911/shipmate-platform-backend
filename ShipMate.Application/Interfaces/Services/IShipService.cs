using ShipMate.Application.DTOs.Ship;

namespace ShipMate.Application.Interfaces.Services;

public interface IShipService
{
    /// <summary>Whether the newest version can ship now, and what stands in the way. Any member.</summary>
    Task<ShipReadinessDto> GetReadinessAsync(Guid userId, Guid workspaceId);

    /// <summary>Manager only: the token the production site must serve.</summary>
    Task<ShipOwnershipDto> GetOwnershipAsync(Guid userId, Guid workspaceId);

    /// <summary>
    /// Manager only. Checks the production URL and, when every check passes, mints the version's Ship Card.
    /// Failed checks are a normal result (the attempt is returned with what failed), not an error.
    /// </summary>
    Task<ShipAttemptResultDto> StartAttemptAsync(
        Guid userId, Guid workspaceId, StartShipAttemptRequest request, CancellationToken cancellationToken = default);

    /// <summary>Manager only: the same version and URL as an earlier attempt, checked again as a new attempt.</summary>
    Task<ShipAttemptResultDto> RecheckAsync(
        Guid userId, Guid workspaceId, Guid attemptId, CancellationToken cancellationToken = default);

    Task<List<ShipAttemptDto>> GetAttemptsAsync(Guid userId, Guid workspaceId);
    Task<List<ShipCardDto>> GetCardsAsync(Guid userId, Guid workspaceId);
    Task<ShipCardDto> GetCardAsync(Guid userId, Guid workspaceId, Guid cardId);
    Task<ShipCardVerificationDto> VerifyCardAsync(Guid userId, Guid workspaceId, Guid cardId);

    /// <summary>Manager only.</summary>
    Task<ShipCardDto> SetVisibilityAsync(Guid userId, Guid workspaceId, Guid cardId, SetShipCardVisibilityRequest request);

    /// <summary>No sign-in. A card that isn't public is not found.</summary>
    Task<PublicShipCardDto> GetPublicCardAsync(string publicSlug);
    Task<ShipCardVerificationDto> VerifyPublicCardAsync(string publicSlug);
}
