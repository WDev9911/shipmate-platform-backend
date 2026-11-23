using System.Globalization;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Exceptions;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;
using ShipMate.Domain.Policies;
using ShipMate.Domain.ValueObjects;

namespace ShipMate.Application.Services;

public class PlatformSettingsService : IPlatformSettingsService
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IAdminActionLogRepository _adminActionLogs;

    public PlatformSettingsService(IPlatformSettingsRepository settings, IAdminActionLogRepository adminActionLogs)
    {
        _settings = settings;
        _adminActionLogs = adminActionLogs;
    }

    public async Task<PlatformSettingsDto> GetAsync() => ToDto(await _settings.GetAsync());

    public async Task<PlatformSettingsDto> UpdateAsync(Guid adminUserId, UpdatePlatformSettingsRequest request)
    {
        var settings = await _settings.GetAsync();

        // The request was made from a copy that is no longer the current one. Saving would overwrite what was changed since.
        if (request.Revision != settings.Revision)
        {
            throw new SettingsChangedByOtherException();
        }

        var patterns = PlatformSettingsLimits.NormalizePatterns(request.MaintenanceCommitPatterns);
        var changes = new List<(string Subject, string Old, string New)>();

        if (request.ComplexityBudgetPerDeveloper != settings.ComplexityBudgetPerDeveloper)
        {
            changes.Add(("complexity_budget_per_developer",
                settings.ComplexityBudgetPerDeveloper.ToString(CultureInfo.InvariantCulture),
                request.ComplexityBudgetPerDeveloper.ToString(CultureInfo.InvariantCulture)));
        }

        if (request.DefaultDriftThreshold != settings.DefaultDriftThreshold)
        {
            changes.Add(("default_drift_threshold",
                settings.DefaultDriftThreshold.ToString("0.00", CultureInfo.InvariantCulture),
                request.DefaultDriftThreshold.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        // Compared by what they say, not by which list object holds them.
        if (!patterns.SequenceEqual(settings.MaintenanceCommitPatterns))
        {
            changes.Add(("maintenance_commit_patterns",
                string.Join(", ", settings.MaintenanceCommitPatterns), string.Join(", ", patterns)));
        }

        if (changes.Count == 0)
        {
            throw new NoChangeException();
        }

        settings.ComplexityBudgetPerDeveloper = request.ComplexityBudgetPerDeveloper;
        settings.DefaultDriftThreshold = request.DefaultDriftThreshold;
        settings.MaintenanceCommitPatterns = patterns;
        settings.Revision++;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = adminUserId;

        // One line for each field that changed, so the log says exactly what moved.
        foreach (var (subject, oldValue, newValue) in changes)
        {
            await _adminActionLogs.AddAsync(new AdminActionLog
            {
                AdminUserId = adminUserId,
                Action = AdminAction.SettingsChanged,
                Subject = subject,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        await _settings.SaveChangesAsync();

        return ToDto(settings);
    }

    public async Task<TestMaintenancePatternsResponse> TestMaintenancePatternsAsync(TestMaintenancePatternsRequest request)
    {
        var patterns = request.Patterns is null
            ? (await _settings.GetAsync()).MaintenanceCommitPatterns
            : PlatformSettingsLimits.NormalizePatterns(request.Patterns);

        var response = new TestMaintenancePatternsResponse
        {
            Messages = request.CommitMessages
                .Select(message => new TestedMessageDto
                {
                    Message = message,
                    Matches = MaintenanceChangePolicy.IsMaintenanceMessage(message, patterns)
                })
                .ToList()
        };

        if (request.Files is not null)
        {
            var files = request.Files.Select(f => new ChangedFile(f.Path, "modified", f.Patch)).ToList();
            response.SkippedByRuleEngine = MaintenanceChangePolicy.IsMaintenanceOnly(request.CommitMessages, files, patterns);
        }

        return response;
    }

    private static PlatformSettingsDto ToDto(PlatformSettings settings) => new()
    {
        ComplexityBudgetPerDeveloper = settings.ComplexityBudgetPerDeveloper,
        DefaultDriftThreshold = settings.DefaultDriftThreshold,
        MaintenanceCommitPatterns = settings.MaintenanceCommitPatterns.ToList(),
        Revision = settings.Revision,
        UpdatedAt = settings.UpdatedAt,
        UpdatedByUserId = settings.UpdatedByUserId,
        Limits = new PlatformSettingsLimitsDto
        {
            MinBudgetPerDeveloper = PlatformSettingsLimits.MinBudgetPerDeveloper,
            MaxBudgetPerDeveloper = PlatformSettingsLimits.MaxBudgetPerDeveloper,
            MinPatterns = PlatformSettingsLimits.MinPatterns,
            MaxPatterns = PlatformSettingsLimits.MaxPatterns,
            MinPatternLength = PlatformSettingsLimits.MinPatternLength,
            MaxPatternLength = PlatformSettingsLimits.MaxPatternLength,
            DriftThresholdHardFloor = DriftThresholdPolicy.HardFloor,
            DriftThresholdMin = DriftThresholdPolicy.Min,
            DriftThresholdMax = DriftThresholdPolicy.Max,
            ManagerMaxThresholdChanges = DriftThresholdPolicy.MaxChanges,
            ManagerThresholdCooldownDays = (int)DriftThresholdPolicy.Cooldown.TotalDays
        }
    };
}
