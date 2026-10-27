using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class NextBestActionService : INextBestActionService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IProductDefinitionRepository _productDefinitionRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IBuildSubmissionRepository _submissionRepository;
    private readonly IDriftAlertRepository _alertRepository;

    public NextBestActionService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IProductDefinitionRepository productDefinitionRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IBuildSubmissionRepository submissionRepository,
        IDriftAlertRepository alertRepository)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _productDefinitionRepository = productDefinitionRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _assignmentRepository = assignmentRepository;
        _submissionRepository = submissionRepository;
        _alertRepository = alertRepository;
    }

    public async Task<NextBestActionDto> GetAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        // Before the first Lock there is no scope to build against.
        var snapshot = await _productDefinitionRepository.GetLatestSnapshotAsync(workspaceId);
        if (snapshot is null)
        {
            return new NextBestActionDto { Action = NextBestActionType.NoAction };
        }

        var features = snapshot.Features.ToDictionary(f => f.FeatureId);
        var assignments = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .Where(a => a.ReleasedAt is null)
            .ToList();
        var openAlerts = await _alertRepository.GetOpenByWorkspaceAsync(workspaceId);
        var openAlertFeatureIds = openAlerts.Select(a => a.FeatureId).ToHashSet();

        var mine = assignments
            .Where(a => a.AssignedUserId == userId && features.TryGetValue(a.FeatureId, out var f) && f.Status == SnapshotFeatureStatus.Locked)
            .Select(a => features[a.FeatureId])
            .OrderBy(f => f.Name)
            .ToList();

        // 1. Drift on your own feature comes first: nothing else of that feature can move until it is dealt with.
        var ownAlert = openAlerts.FirstOrDefault(a => mine.Any(f => f.FeatureId == a.FeatureId));
        if (ownAlert is not null)
        {
            return ForFeature(NextBestActionType.FixDrift, features[ownAlert.FeatureId], alertId: ownAlert.Id);
        }

        // 2. Code that already passed the check waits for your approval.
        var needsWork = new List<SnapshotFeature>();
        foreach (var feature in mine)
        {
            var newest = await _submissionRepository.GetLatestAsync(workspaceId, feature.FeatureId);
            if (newest is not null)
            {
                var latestAlert = await _alertRepository.GetLatestByFeatureAsync(workspaceId, feature.FeatureId);
                if (BuildApprovalPolicy.Evaluate(newest, newest, hasOpenAlert: false, latestAlert?.Status) == ApprovalReadiness.Ready)
                {
                    return ForFeature(NextBestActionType.ApproveSubmission, feature, submissionId: newest.Id);
                }
            }

            // Approved code is done until the developer submits more; the rest still has work in it.
            if (newest is null || newest.Status != BuildSubmissionStatus.Approved)
            {
                needsWork.Add(feature);
            }
        }

        Blocker? BlockerOf(Guid featureId) => FindBlocker(snapshot, features, openAlertFeatureIds, featureId);

        // 3. A feature of yours that nothing holds up.
        var workable = needsWork.FirstOrDefault(f => BlockerOf(f.FeatureId) is null);
        if (workable is not null)
        {
            return ForFeature(NextBestActionType.WorkOnFeature, workable);
        }

        // 4. Nothing of yours to do: a free feature your skill covers.
        var membership = await _workspaceMemberRepository.GetByWorkspaceAndUserIdAsync(workspaceId, userId);
        var claimable = FindClaimableFeature(snapshot, assignments, membership?.SkillLevel, f => BlockerOf(f) is null);
        if (claimable is not null)
        {
            return ForFeature(NextBestActionType.ClaimFeature, claimable);
        }

        // 5. What is left of yours is waiting on another feature.
        foreach (var feature in needsWork)
        {
            if (BlockerOf(feature.FeatureId) is { } blocker)
            {
                var blocked = ForFeature(NextBestActionType.Blocked, feature);
                blocked.BlockedReason = blocker.Reason;
                blocked.BlockedByFeatureId = blocker.ByFeatureId;
                return blocked;
            }
        }

        return new NextBestActionDto { Action = NextBestActionType.NoAction };
    }

    // The free locked feature that most others wait for, so taking it frees the most work; ties go by name.
    private static SnapshotFeature? FindClaimableFeature(
        MvpSnapshot snapshot, List<FeatureAssignment> assignments, int? skill, Func<Guid, bool> isNotBlocked)
    {
        if (skill is null)
        {
            return null;
        }

        var taken = assignments.Select(a => a.FeatureId).ToHashSet();
        return snapshot.Features
            .Where(f => f.Status == SnapshotFeatureStatus.Locked
                && !taken.Contains(f.FeatureId)
                && f.Complexity <= skill
                && isNotBlocked(f.FeatureId))
            .OrderByDescending(f => snapshot.Dependencies.Count(d => d.DependsOnFeatureId == f.FeatureId))
            .ThenBy(f => f.Name)
            .FirstOrDefault();
    }

    // A feature is held up by a feature it depends on: a backup nobody took, or one whose drift nobody dealt with.
    private static Blocker? FindBlocker(
        MvpSnapshot snapshot, Dictionary<Guid, SnapshotFeature> features, HashSet<Guid> featuresWithOpenAlert, Guid featureId)
    {
        var prerequisites = snapshot.Dependencies
            .Where(d => d.FeatureId == featureId && features.ContainsKey(d.DependsOnFeatureId))
            .Select(d => features[d.DependsOnFeatureId])
            .ToList();

        var backup = prerequisites.FirstOrDefault(p => p.Status == SnapshotFeatureStatus.Backup);
        if (backup is not null)
        {
            return new Blocker(NextBestActionBlockedReasons.DependencyPendingAssignment, backup.FeatureId);
        }

        var drifting = prerequisites.FirstOrDefault(p => featuresWithOpenAlert.Contains(p.FeatureId));
        return drifting is null ? null : new Blocker(NextBestActionBlockedReasons.DependencyDriftUnresolved, drifting.FeatureId);
    }

    private static NextBestActionDto ForFeature(
        NextBestActionType action, SnapshotFeature feature, Guid? alertId = null, Guid? submissionId = null) => new()
    {
        Action = action,
        FeatureId = feature.FeatureId,
        FeatureName = feature.Name,
        AlertId = alertId,
        SubmissionId = submissionId
    };

    private sealed record Blocker(string Reason, Guid ByFeatureId);
}
