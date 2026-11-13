using ShipMate.Application.DTOs.Verify;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Common;
using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Services;

public class VerificationService : IVerificationService
{
    private readonly IWorkspaceAccessGuard _workspaceAccessGuard;
    private readonly IVerificationChecklistRepository _checklistRepository;
    private readonly IBuildSubmissionRepository _submissionRepository;
    private readonly IDriftAlertRepository _alertRepository;
    private readonly IAuditTrail _auditTrail;

    public VerificationService(
        IWorkspaceAccessGuard workspaceAccessGuard,
        IVerificationChecklistRepository checklistRepository,
        IBuildSubmissionRepository submissionRepository,
        IDriftAlertRepository alertRepository,
        IAuditTrail auditTrail)
    {
        _workspaceAccessGuard = workspaceAccessGuard;
        _checklistRepository = checklistRepository;
        _submissionRepository = submissionRepository;
        _alertRepository = alertRepository;
        _auditTrail = auditTrail;
    }

    public async Task<List<VerificationChecklistSummaryDto>> GetAllAsync(Guid userId, Guid workspaceId)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        var checklists = await _checklistRepository.GetByWorkspaceIdAsync(workspaceId);
        return checklists.Select((c, index) => new VerificationChecklistSummaryDto
        {
            Version = c.SnapshotVersion,
            IsLatest = index == 0,
            AllItemsChecked = c.AllItemsChecked,
            MissingCount = c.MissingItems().Count,
            CompletedAt = c.CompletedAt
        }).ToList();
    }

    public async Task<VerificationChecklistDto> GetAsync(Guid userId, Guid workspaceId, int version)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);

        return await ToDtoAsync(await GetChecklistOrThrow(workspaceId, version));
    }

    public async Task<VerificationChecklistDto> CheckItemAsync(
        Guid userId, Guid workspaceId, int version, string itemKey, CheckVerificationItemRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsMemberAsync(userId, workspaceId);
        RequireKnown(itemKey);

        var checklist = await GetChecklistOrThrow(workspaceId, version);

        var result = checklist.Check(itemKey, request.Checked, request.Note, userId, DateTime.UtcNow);
        ThrowIfRefused(result);

        if (result.Transition == ChecklistTransition.Completed)
        {
            await AppendCompletedAsync(workspaceId, userId, checklist);
        }

        // The list and its audit records are saved in one go.
        await _checklistRepository.SaveChangesAsync();

        return await ToDtoAsync(checklist);
    }

    public async Task<VerificationChecklistDto> SetApplicabilityAsync(
        Guid userId, Guid workspaceId, int version, string itemKey, SetVerificationItemApplicabilityRequest request)
    {
        await _workspaceAccessGuard.GetWorkspaceAsManagerAsync(userId, workspaceId);
        RequireKnown(itemKey);

        var checklist = await GetChecklistOrThrow(workspaceId, version);
        var wasApplicable = checklist.Items.Single(i => i.ItemKey == itemKey).Applicable;
        var reason = request.Reason?.Trim();

        var result = checklist.SetApplicability(itemKey, request.Applicable, reason, DateTime.UtcNow);
        ThrowIfRefused(result);

        // Skipping a check (or bringing it back) changes what "verified" means, so it leaves a record.
        // Saying again what is already so changes nothing and leaves none.
        if (wasApplicable != request.Applicable)
        {
            await _auditTrail.AppendAsync(workspaceId, AuditTypes.VerifyItemNotApplicable, userId, new
            {
                version = checklist.SnapshotVersion,
                itemKey,
                applicable = request.Applicable,
                reason = request.Applicable ? null : reason
            });
        }

        if (result.Transition == ChecklistTransition.Completed)
        {
            await AppendCompletedAsync(workspaceId, userId, checklist);
        }

        await _checklistRepository.SaveChangesAsync();

        return await ToDtoAsync(checklist);
    }

    public async Task<VerificationReadiness> GetReadinessAsync(Guid workspaceId, int version)
    {
        var checklist = await _checklistRepository.GetByVersionAsync(workspaceId, version);
        if (checklist is null)
        {
            return VerificationReadiness.Missing;
        }

        return new VerificationReadiness(
            Exists: true,
            checklist.AllItemsChecked,
            checklist.MissingItems(),
            checklist.CompletedAt,
            checklist.Id);
    }

    private async Task<VerificationChecklist> GetChecklistOrThrow(Guid workspaceId, int version) =>
        await _checklistRepository.GetByVersionAsync(workspaceId, version)
            ?? throw new NotFoundException("Verification checklist", version);

    private static void RequireKnown(string itemKey)
    {
        if (!VerificationItemKeys.IsKnown(itemKey))
        {
            throw new VerificationItemUnknownException();
        }
    }

    private static void ThrowIfRefused(ChecklistEditResult result)
    {
        switch (result.Error)
        {
            case ChecklistEditError.ItemNotApplicable:
                throw new VerificationItemNotApplicableException();
            case ChecklistEditError.MainJourneyRequired:
                throw new VerificationMainJourneyRequiredException();
        }
    }

    // The record keeps what the team ticked and what it skipped, so the finished list can be read back later.
    private async Task AppendCompletedAsync(Guid workspaceId, Guid userId, VerificationChecklist checklist) =>
        await _auditTrail.AppendAsync(workspaceId, AuditTypes.VerifyCompleted, userId, new
        {
            version = checklist.SnapshotVersion,
            checklistId = checklist.Id,
            items = checklist.Items
                .OrderBy(i => VerificationItemKeys.All.ToList().IndexOf(i.ItemKey))
                .Select(i => new { key = i.ItemKey, applicable = i.Applicable, @checked = i.Checked })
        });

    private async Task<VerificationChecklistDto> ToDtoAsync(VerificationChecklist checklist)
    {
        var lockedFeatures = checklist.MvpSnapshot.Features
            .Where(f => f.Status == SnapshotFeatureStatus.Locked)
            .ToList();

        return new VerificationChecklistDto
        {
            Version = checklist.SnapshotVersion,
            AllItemsChecked = checklist.AllItemsChecked,
            CompletedAt = checklist.CompletedAt,
            MissingItems = checklist.MissingItems().ToList(),
            Items = checklist.Items
                .OrderBy(i => VerificationItemKeys.All.ToList().IndexOf(i.ItemKey))
                .Select(i => new VerificationItemDto
                {
                    Key = i.ItemKey,
                    Applicable = i.Applicable,
                    NotApplicableReason = i.NotApplicableReason,
                    Checked = i.Checked,
                    Note = i.Note,
                    CheckedByUserId = i.CheckedByUserId,
                    CheckedAt = i.CheckedAt
                })
                .ToList(),
            Info = await InfoAsync(checklist.WorkspaceId, lockedFeatures)
        };
    }

    // An alert counts while its feature is still locked in this version, whichever version raised it:
    // an alert from v1 that nobody dealt with is still open when v2 comes.
    private async Task<VerificationInfoDto> InfoAsync(Guid workspaceId, List<SnapshotFeature> lockedFeatures)
    {
        var lockedIds = lockedFeatures.Select(f => f.FeatureId).ToHashSet();
        var openDrift = (await _alertRepository.GetOpenByWorkspaceAsync(workspaceId))
            .Count(a => lockedIds.Contains(a.FeatureId));

        var approved = 0;
        foreach (var feature in lockedFeatures)
        {
            var newest = await _submissionRepository.GetLatestAsync(workspaceId, feature.FeatureId);
            if (newest?.Status == BuildSubmissionStatus.Approved)
            {
                approved++;
            }
        }

        return new VerificationInfoDto
        {
            OpenDriftCount = openDrift,
            ApprovedFeatures = approved,
            LockedFeatures = lockedFeatures.Count
        };
    }
}
