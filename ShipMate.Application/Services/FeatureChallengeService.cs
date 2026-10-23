using AutoMapper;
using ShipMate.Application.Ai.Define;
using ShipMate.Application.Ai.Lock;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class FeatureChallengeService : IFeatureChallengeService
{
    private readonly IFeatureChallengeRepository _challengeRepository;
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IAiGateway _aiGateway;
    private readonly IMapper _mapper;

    public FeatureChallengeService(
        IFeatureChallengeRepository challengeRepository,
        IProductDefinitionRepository productDefinitionRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IWorkspaceAccessGuard workspaceAccessGuard,
        IAiGateway aiGateway,
        IMapper mapper)
    {
        _challengeRepository = challengeRepository;
        _productDefinitionRepository = productDefinitionRepository;
        _assignmentRepository = assignmentRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceAccessGuard = workspaceAccessGuard;
        _aiGateway = aiGateway;
        _mapper = mapper;
    }

    public async Task<FeatureChallengeDto> SubmitAsync(
        Guid userId, Guid workspaceId, SubmitChallengeRequest request, CancellationToken cancellationToken = default)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var snapshot = await GetSnapshotOrThrow(workspaceId);
        var productDefinition = await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId);

        var challenge = new FeatureChallenge
        {
            WorkspaceId = workspaceId,
            RequestedByUserId = userId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Reason = request.Reason.Trim()
        };

        var scoring = await ComplexityScoringClient.ScoreAsync(_aiGateway, new ComplexityScoringInput
        {
            Problem = productDefinition?.Problem,
            Solution = productDefinition?.Solution,
            Features =
            [
                new ComplexityScoringFeatureInput
                {
                    Id = challenge.FeatureId.ToString(),
                    Name = challenge.Name,
                    Description = challenge.Description
                }
            ]
        }, cancellationToken);
        challenge.ProposedComplexity = scoring.Scores[0].Complexity;

        await _challengeRepository.AddAsync(challenge);
        await _challengeRepository.SaveChangesAsync();

        return await ToDtoAsync(challenge, snapshot);
    }

    public async Task<FeatureChallengeDto> ConfirmComplexityAsync(
        Guid userId, Guid workspaceId, Guid challengeId, ConfirmChallengeComplexityRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var challenge = await GetChallengeOrThrow(workspaceId, challengeId);
        if (challenge.Status != ChallengeStatus.Open)
        {
            throw new ChallengeAlreadyDecidedException();
        }

        var membership = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, userId);
        if (challenge.RequestedByUserId != userId && membership?.Role != WorkspaceMemberRole.Manager)
        {
            throw new WorkspacePermissionDeniedException();
        }

        challenge.Complexity = request.Complexity;
        await _challengeRepository.SaveChangesAsync();

        return await ToDtoAsync(challenge, await GetSnapshotOrThrow(workspaceId));
    }

    public async Task<List<FeatureChallengeDto>> GetAllAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var snapshot = await GetSnapshotOrThrow(workspaceId);
        var challenges = await _challengeRepository.GetByWorkspaceIdAsync(workspaceId);

        var result = new List<FeatureChallengeDto>();
        foreach (var challenge in challenges)
        {
            result.Add(await ToDtoAsync(challenge, snapshot));
        }

        return result;
    }

    public async Task<FeatureChallengeDto> GetAsync(Guid userId, Guid workspaceId, Guid challengeId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var challenge = await GetChallengeOrThrow(workspaceId, challengeId);
        return await ToDtoAsync(challenge, await GetSnapshotOrThrow(workspaceId));
    }

    public async Task<FeatureChallengeDto> ApproveAsync(
        Guid userId, Guid workspaceId, Guid challengeId, ApproveChallengeRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var challenge = await GetChallengeOrThrow(workspaceId, challengeId);
        if (challenge.Status != ChallengeStatus.Open)
        {
            throw new ChallengeAlreadyDecidedException();
        }

        if (challenge.Complexity is null)
        {
            throw new ChallengeComplexityNotConfirmedException();
        }

        var snapshot = await GetSnapshotOrThrow(workspaceId);
        var skills = await GetActiveMemberSkillsAsync(workspaceId);
        var next = MvpSnapshotEvolution.NextVersion(snapshot, userId, skills.Count);
        var assignments = await GetActiveAssignmentsAsync(workspaceId);

        var removedCommitted = false;
        switch (request.Action)
        {
            case ChallengeAction.AddDirectly:
                MvpSnapshotEvolution.AddFeature(next, challenge, SnapshotFeatureStatus.Locked);
                break;

            case ChallengeAction.Backup:
                if (challenge.Complexity <= skills.Where(s => s is not null).Select(s => s!.Value).DefaultIfEmpty(0).Max())
                {
                    throw new BackupNotApplicableException();
                }

                MvpSnapshotEvolution.AddFeature(next, challenge, SnapshotFeatureStatus.Backup);
                break;

            case ChallengeAction.RemoveAndReplace:
                removedCommitted = ApplyRemoveAndReplace(snapshot, next, challenge, request, assignments);
                break;
        }

        var gate = LockGatePolicy.Evaluate(MvpSnapshotEvolution.ActiveFeatures(next), skills);
        if (!gate.IsPassed)
        {
            throw new LockGateFailedException(LockGateDto.From(gate));
        }

        next.Hash = MvpSnapshotHasher.Compute(next);
        await _productDefinitionRepository.AddSnapshotAsync(next);

        var now = DateTime.UtcNow;
        ReleaseReplacedAssignments(snapshot, next, assignments, now);

        challenge.Status = ChallengeStatus.Approved;
        challenge.DecidedByUserId = userId;
        challenge.DecidedAt = now;
        challenge.Action = request.Action;
        challenge.ResultingSnapshotVersion = next.Version;
        if (request.Action == ChallengeAction.RemoveAndReplace)
        {
            challenge.RemovedFeatureId = request.RemoveFeatureId;
            challenge.DependentsResolution = request.DependentsResolution;
            challenge.ReplacementFeatureId = request.ReplacementFeatureId;
            challenge.CommittedRemovalConfirmed = removedCommitted && request.ConfirmCommittedRemoval;
        }

        await _challengeRepository.SaveChangesAsync();

        return await ToDtoAsync(challenge, next);
    }

    // Returns whether the removed feature came from the committed list.
    private static bool ApplyRemoveAndReplace(
        MvpSnapshot snapshot, MvpSnapshot next, FeatureChallenge challenge, ApproveChallengeRequest request,
        IReadOnlyList<FeatureAssignment> assignments)
    {
        var removedId = request.RemoveFeatureId!.Value;
        var removed = snapshot.Features.FirstOrDefault(f => f.FeatureId == removedId && f.Status != SnapshotFeatureStatus.Replaced)
            ?? throw new NotFoundException("Feature", removedId);

        var removedCommitted = removed.Origin == FeatureOrigin.FromCommittedList;
        if (removedCommitted && !request.ConfirmCommittedRemoval)
        {
            throw new CommittedRemovalNotConfirmedException();
        }

        var error = MvpSnapshotEvolution.RemoveAndReplace(
            next, challenge, removedId, request.DependentsResolution, request.ReplacementFeatureId);

        switch (error)
        {
            case EvolutionError.DependentsResolutionRequired:
                throw new DependentsResolutionRequiredException(
                    Affected(snapshot, MvpSnapshotEvolution.DependentsOf(snapshot, removedId), assignments));
            case EvolutionError.ReplacementInvalid:
                throw new InvalidReplacementException();
            case EvolutionError.RemovedFeatureNotFound:
                throw new NotFoundException("Feature", removedId);
        }

        return removedCommitted;
    }

    public async Task<ReplacementCheckDto> CheckReplacementAsync(
        Guid userId, Guid workspaceId, Guid challengeId, ReplacementCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var challenge = await GetChallengeOrThrow(workspaceId, challengeId);
        if (challenge.Status != ChallengeStatus.Open)
        {
            throw new ChallengeAlreadyDecidedException();
        }

        var snapshot = await GetSnapshotOrThrow(workspaceId);
        var replacement = snapshot.Features.FirstOrDefault(f =>
                f.FeatureId == request.ReplacementFeatureId && f.Status != SnapshotFeatureStatus.Replaced)
            ?? throw new InvalidReplacementException();

        var persona = (await _productDefinitionRepository.GetByWorkspaceIdAsync(workspaceId))?.LockedPersona
            ?? throw new ProductDefinitionNotAnalyzedException();

        var result = await PersonaConflictClient.CheckAsync(_aiGateway, new PersonaConflictInput
        {
            Persona = new DefinePersona
            {
                PrimaryPersona = persona.PrimaryPersona,
                Reason = persona.Reason,
                SupportingRoles = persona.SupportingRoles
                    .Select(r => new DefineSupportingRole { Name = r.Name, Reason = r.Reason })
                    .ToList()
            },
            Feature = new PersonaConflictFeatureInput
            {
                Name = replacement.Name,
                Description = replacement.Description,
                Scope = replacement.Scope
            }
        }, cancellationToken);

        challenge.PersonaCheckFeatureId = replacement.FeatureId;
        challenge.PersonaConflict = result.PersonaConflict;
        challenge.PersonaConflictReason = result.Reason;
        await _challengeRepository.SaveChangesAsync();

        return new ReplacementCheckDto(replacement.FeatureId, result.PersonaConflict, result.Reason);
    }

    public async Task<FeatureChallengeDto> RejectAsync(
        Guid userId, Guid workspaceId, Guid challengeId, RejectChallengeRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var challenge = await GetChallengeOrThrow(workspaceId, challengeId);
        if (challenge.Status != ChallengeStatus.Open)
        {
            throw new ChallengeAlreadyDecidedException();
        }

        // A rejected challenge changes nothing in the snapshot; the row itself is the record.
        challenge.Status = ChallengeStatus.Rejected;
        challenge.DecidedByUserId = userId;
        challenge.DecidedAt = DateTime.UtcNow;
        challenge.RejectionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        await _challengeRepository.SaveChangesAsync();

        return await ToDtoAsync(challenge, await GetSnapshotOrThrow(workspaceId));
    }

    private async Task<FeatureChallengeDto> ToDtoAsync(FeatureChallenge challenge, MvpSnapshot snapshot)
    {
        var dto = _mapper.Map<FeatureChallengeDto>(challenge);

        if (challenge.Status == ChallengeStatus.Open && challenge.Complexity is not null)
        {
            var skills = await GetActiveMemberSkillsAsync(challenge.WorkspaceId);
            var assignments = await GetActiveAssignmentsAsync(challenge.WorkspaceId);
            var recommendation = ChallengeRecommendationPolicy.Recommend(
                MvpSnapshotEvolution.ActiveFeatures(snapshot), challenge.Complexity.Value, skills);

            dto.Recommendation = new ChallengeRecommendationDto
            {
                Action = recommendation.Action,
                FeatureToRemove = recommendation.FeatureToRemove is { } candidate
                    ? new RemovalCandidateDto(candidate.Id, candidate.Name, candidate.Complexity!.Value,
                        candidate.IsCommitted, AssigneeOf(candidate.Id, assignments))
                    : null,
                AffectedDependents = recommendation.FeatureToRemove is { } removal
                    ? Affected(snapshot, MvpSnapshotEvolution.DependentsOf(snapshot, removal.Id), assignments)
                    : new List<AffectedFeatureDto>()
            };
        }

        return dto;
    }

    // A feature replaced by this approval is no longer held by anyone; its assignment stays as history.
    private static void ReleaseReplacedAssignments(
        MvpSnapshot previous, MvpSnapshot next, IEnumerable<FeatureAssignment> assignments, DateTime now)
    {
        var alreadyReplaced = previous.Features
            .Where(f => f.Status == SnapshotFeatureStatus.Replaced)
            .Select(f => f.FeatureId)
            .ToHashSet();
        var replacedNow = next.Features
            .Where(f => f.Status == SnapshotFeatureStatus.Replaced && !alreadyReplaced.Contains(f.FeatureId))
            .Select(f => f.FeatureId)
            .ToHashSet();

        foreach (var assignment in assignments.Where(a => replacedNow.Contains(a.FeatureId)))
        {
            assignment.ReleasedAt = now;
        }
    }

    private static List<AffectedFeatureDto> Affected(
        MvpSnapshot snapshot, IEnumerable<Guid> featureIds, IReadOnlyList<FeatureAssignment> assignments)
    {
        var namesById = snapshot.Features.ToDictionary(f => f.FeatureId, f => f.Name);
        return featureIds
            .Select(id => new AffectedFeatureDto(id, namesById[id], AssigneeOf(id, assignments)))
            .ToList();
    }

    private static string? AssigneeOf(Guid featureId, IReadOnlyList<FeatureAssignment> assignments) =>
        assignments.FirstOrDefault(a => a.FeatureId == featureId)?.AssignedUser.DisplayName;

    private async Task<List<FeatureAssignment>> GetActiveAssignmentsAsync(Guid workspaceId) =>
        (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(a => a.ReleasedAt is null)
            .ToList();

    private async Task<MvpSnapshot> GetSnapshotOrThrow(Guid workspaceId) =>
        await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId)
            ?? throw new NotFoundException("MVP snapshot", workspaceId);

    private async Task<FeatureChallenge> GetChallengeOrThrow(Guid workspaceId, Guid challengeId) =>
        await _challengeRepository.GetByIdAsync(workspaceId, challengeId)
            ?? throw new NotFoundException("Feature challenge", challengeId);

    private async Task<List<int?>> GetActiveMemberSkillsAsync(Guid workspaceId) =>
        (await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(m => m.Status == WorkspaceMemberStatus.Active)
            .Select(m => m.SkillLevel)
            .ToList();
}
