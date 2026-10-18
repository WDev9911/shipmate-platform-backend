using ShipMate.Application.DTOs.ProductDefinitions;

namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// LOCK Feature Challenge: after the first Lock, scope changes go through a request that the AI scores, the
/// requester confirms and the Manager decides. An approval produces the next snapshot version.
/// </summary>
public interface IFeatureChallengeService
{
    /// <summary>Records the request and asks the AI to propose the new feature's complexity.</summary>
    Task<FeatureChallengeDto> SubmitAsync(
        Guid userId, Guid workspaceId, SubmitChallengeRequest request, CancellationToken cancellationToken = default);

    /// <summary>The requester (or the Manager) confirms or adjusts the AI's complexity.</summary>
    Task<FeatureChallengeDto> ConfirmComplexityAsync(
        Guid userId, Guid workspaceId, Guid challengeId, ConfirmChallengeComplexityRequest request);

    Task<List<FeatureChallengeDto>> GetAllAsync(Guid userId, Guid workspaceId);

    Task<FeatureChallengeDto> GetAsync(Guid userId, Guid workspaceId, Guid challengeId);

    Task<FeatureChallengeDto> ApproveAsync(Guid userId, Guid workspaceId, Guid challengeId, ApproveChallengeRequest request);

    /// <summary>
    /// Before approving a Restructure onto an existing feature, checks that feature against the locked persona.
    /// Only a warning for the Manager; the result is kept on the challenge.
    /// </summary>
    Task<ReplacementCheckDto> CheckReplacementAsync(
        Guid userId, Guid workspaceId, Guid challengeId, ReplacementCheckRequest request, CancellationToken cancellationToken = default);

    Task<FeatureChallengeDto> RejectAsync(Guid userId, Guid workspaceId, Guid challengeId, RejectChallengeRequest request);
}
