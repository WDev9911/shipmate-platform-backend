using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class DriftAlertService : IDriftAlertService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IDriftAlertRepository _alertRepository;
    private readonly IFeatureAssignmentRepository _assignmentRepository;
    private readonly IWorkspaceMemberRepository _workspaceMemberRepository;
    private readonly IAuditTrail _auditTrail;
    private readonly IWorkspaceNotifier _notifier;

    public DriftAlertService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IDriftAlertRepository alertRepository,
        IFeatureAssignmentRepository assignmentRepository,
        IWorkspaceMemberRepository workspaceMemberRepository,
        IAuditTrail auditTrail,
        IWorkspaceNotifier notifier)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _alertRepository = alertRepository;
        _assignmentRepository = assignmentRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _auditTrail = auditTrail;
        _notifier = notifier;
    }

    public async Task<List<DriftAlertDto>> GetAllAsync(Guid userId, Guid workspaceId, DriftAlertStatus? status)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var alerts = await _alertRepository.GetByWorkspaceIdAsync(workspaceId, status);
        return alerts.Select(ToDto).ToList();
    }

    public async Task<DriftAlertDto> AcknowledgeAsync(Guid userId, Guid workspaceId, Guid alertId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var alert = await GetAlertOrThrow(workspaceId, alertId);

        var holdsFeature = (await _assignmentRepository.GetByWorkspaceIdAsync(workspaceId))
            .Any(a => a.FeatureId == alert.FeatureId && a.ReleasedAt is null && a.AssignedUserId == userId);
        if (!holdsFeature)
        {
            throw new NotFeatureHolderException();
        }

        if (!alert.Acknowledge(userId, DateTime.UtcNow))
        {
            throw new DriftAlertNotOpenException();
        }

        await _alertRepository.SaveChangesAsync();

        await _notifier.PublishAsync(workspaceId, WorkspaceEvents.DriftAlertAcknowledged, new
        {
            alertId = alert.Id,
            featureId = alert.FeatureId,
            acknowledgedBy = userId
        });

        return ToDto(alert);
    }

    public async Task<DriftAlertDto> OverrideAsync(
        Guid userId, Guid workspaceId, Guid alertId, OverrideDriftAlertRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        var alert = await GetAlertOrThrow(workspaceId, alertId);
        var reason = request.Reason.Trim();
        if (!alert.Override(userId, reason, DateTime.UtcNow))
        {
            throw new DriftAlertNotOpenException();
        }

        await _auditTrail.AppendAsync(workspaceId, AuditTypes.ScopeBypass, userId, new
        {
            alertId = alert.Id,
            featureId = alert.FeatureId,
            submissionId = alert.SubmissionId,
            reason,
            confidenceScore = alert.ConfidenceScore,
            thresholdAtCheck = alert.ThresholdAtCheck,
            snapshotVersionAtCheck = alert.SnapshotVersionAtCheck
        });

        // A lone Manager is both the one who drifted and the one who approves it: the record says so, for whoever reads it later.
        var activeMembers = (await _workspaceMemberRepository.GetByWorkspaceIdAsync(workspaceId))
            .Count(m => m.Status == WorkspaceMemberStatus.Active);
        if (activeMembers == 1)
        {
            await _auditTrail.AppendAsync(workspaceId, AuditTypes.SoloOverrideLogged, userId, new { alertId = alert.Id });
        }

        // The alert and its audit records are saved in one go.
        await _alertRepository.SaveChangesAsync();

        await _notifier.PublishAsync(workspaceId, WorkspaceEvents.ManagerOverrideExecuted, new
        {
            alertId = alert.Id,
            featureId = alert.FeatureId,
            overriddenBy = userId,
            reason,
            solo = activeMembers == 1
        });
        return ToDto(alert);
    }

    private async Task<DriftAlert> GetAlertOrThrow(Guid workspaceId, Guid alertId) =>
        await _alertRepository.GetByIdAsync(workspaceId, alertId)
            ?? throw new NotFoundException("Drift alert", alertId);

    private static DriftAlertDto ToDto(DriftAlert alert) => new()
    {
        Id = alert.Id,
        FeatureId = alert.FeatureId,
        SubmissionId = alert.SubmissionId,
        SnapshotVersionAtCheck = alert.SnapshotVersionAtCheck,
        ThresholdAtCheck = alert.ThresholdAtCheck,
        ConfidenceScore = alert.ConfidenceScore,
        SuspectedFeatureIds = alert.SuspectedFeatureIds,
        Status = alert.Status,
        AcknowledgedAt = alert.AcknowledgedAt,
        AcknowledgedByUserId = alert.AcknowledgedByUserId,
        OverriddenAt = alert.OverriddenAt,
        OverriddenByUserId = alert.OverriddenByUserId,
        OverrideReason = alert.OverrideReason,
        ResolvedAt = alert.ResolvedAt,
        ResolvedBySubmissionId = alert.ResolvedBySubmissionId,
        CreatedAt = alert.CreatedAt
    };
}
