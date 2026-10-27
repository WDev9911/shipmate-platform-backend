using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Policies;

namespace ShipMate.Application.Services;

public class DriftThresholdService : IDriftThresholdService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IAuditTrailRepository _auditTrailRepository;
    private readonly IAuditTrail _auditTrail;

    public DriftThresholdService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IWorkspaceRepository workspaceRepository,
        IAuditTrailRepository auditTrailRepository,
        IAuditTrail auditTrail)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _workspaceRepository = workspaceRepository;
        _auditTrailRepository = auditTrailRepository;
        _auditTrail = auditTrail;
    }

    public async Task<DriftThresholdDto> ChangeAsync(Guid userId, Guid workspaceId, ChangeDriftThresholdRequest request)
    {
        var workspace = await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);

        // The count comes from the audit trail, which can't be edited, and not from anything the client sends.
        var previous = (await _auditTrailRepository.GetByTypeAsync(workspaceId, AuditTypes.ThresholdChanged))
            .Select(e => e.CreatedAt)
            .ToList();
        var now = DateTime.UtcNow;
        var current = workspace.DriftConfidenceThreshold;

        switch (DriftThresholdPolicy.Check(current, request.Value, previous, now))
        {
            case ThresholdChangeVerdict.OutOfRange:
                throw new ThresholdOutOfRangeException();
            case ThresholdChangeVerdict.Unchanged:
                throw new ThresholdUnchangedException();
            case ThresholdChangeVerdict.LimitReached:
                throw new ThresholdChangeLimitReachedException();
            case ThresholdChangeVerdict.CoolingDown:
                throw new ThresholdChangeCooldownException(DriftThresholdPolicy.NextAllowedChange(previous));
        }

        workspace.DriftConfidenceThreshold = request.Value;
        workspace.UpdatedAt = now;
        _workspaceRepository.Update(workspace);

        await _auditTrail.AppendAsync(workspaceId, AuditTypes.ThresholdChanged, userId, new
        {
            from = current,
            to = request.Value,
            changeNumber = previous.Count + 1
        });

        // Lowering is the move that lets drift through, so it gets its own record for SHIP to score.
        if (request.Value < current)
        {
            await _auditTrail.AppendAsync(workspaceId, AuditTypes.GovernanceDilution, userId, new
            {
                from = current,
                to = request.Value
            });
        }

        // The new value and its audit records are saved together; a racing change fails here instead of both winning.
        await _workspaceRepository.SaveChangesAsync();

        return new DriftThresholdDto
        {
            Value = request.Value,
            ChangesUsed = previous.Count + 1,
            MaxChanges = DriftThresholdPolicy.MaxChanges,
            NextChangeAllowedAt = previous.Count + 1 >= DriftThresholdPolicy.MaxChanges ? null : now + DriftThresholdPolicy.Cooldown
        };
    }
}
