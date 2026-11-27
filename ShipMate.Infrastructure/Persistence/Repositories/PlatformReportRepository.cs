using Microsoft.EntityFrameworkCore;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Infrastructure.Persistence.Repositories;

public class PlatformReportRepository : IPlatformReportRepository
{
    private static readonly TimeSpan NewUserWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(10);

    // The governance events an administrator follows; the other audit types are the ordinary life of a workspace.
    private static readonly string[] GovernanceAuditTypes =
    [
        AuditTypes.GovernanceDilution, AuditTypes.ScopeBypass, AuditTypes.SoloOverrideLogged,
        AuditTypes.DirectPushDetected, AuditTypes.SystemAlert
    ];

    private readonly AppDbContext _context;

    public PlatformReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiUsageRowDto>> GetAiUsageAsync(DateTime fromUtc, DateTime toUtc) =>
        await _context.AiCallLogs
            .AsNoTracking()
            .Where(l => l.CreatedAt >= fromUtc && l.CreatedAt < toUtc)
            .GroupBy(l => new { Day = l.CreatedAt.Date, l.Purpose })
            .Select(g => new AiUsageRowDto
            {
                Day = g.Key.Day,
                Purpose = g.Key.Purpose,
                Calls = g.Count(),
                Succeeded = g.Count(l => l.Outcome == AiCallOutcome.Succeeded),
                RateLimited = g.Count(l => l.Outcome == AiCallOutcome.RateLimited),
                ProviderUnavailable = g.Count(l => l.Outcome == AiCallOutcome.ProviderUnavailable),
                InvalidOutput = g.Count(l => l.Outcome == AiCallOutcome.InvalidOutput),
                Cancelled = g.Count(l => l.Outcome == AiCallOutcome.Cancelled),
                InputTokens = g.Sum(l => (long?)l.InputTokens) ?? 0,
                OutputTokens = g.Sum(l => (long?)l.OutputTokens) ?? 0,
                ThinkingTokens = g.Sum(l => (long?)l.ThinkingTokens) ?? 0,
                AverageDurationMs = g.Average(l => (double)l.DurationMs)
            })
            .OrderBy(r => r.Day)
            .ThenBy(r => r.Purpose)
            .ToListAsync();

    public async Task<PlatformOverviewDto> GetOverviewAsync(DateTime nowUtc)
    {
        var usersByRole = await _context.Users.GroupBy(u => u.Role)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var usersByStatus = await _context.Users.GroupBy(u => u.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var newSince = nowUtc - NewUserWindow;
        var newUsers = await _context.Users.CountAsync(u => u.CreatedAt >= newSince);

        var workspaces = await _context.Workspaces.GroupBy(w => w.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        var submissions = await _context.BuildSubmissions.GroupBy(s => s.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var stuckBefore = nowUtc - StuckAfter;
        var stuck = await _context.BuildSubmissions
            .CountAsync(s => s.Status == BuildSubmissionStatus.Analyzing && s.CreatedAt < stuckBefore);

        var alerts = await _context.DriftAlerts.GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        var audit = await _context.AuditTrail
            .Where(e => GovernanceAuditTypes.Contains(e.AuditType))
            .GroupBy(e => e.AuditType)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        return new PlatformOverviewDto
        {
            GeneratedAt = nowUtc,
            UsersByRole = ByName(usersByRole.ToDictionary(r => r.Key, r => r.Count)),
            UsersByStatus = ByName(usersByStatus.ToDictionary(r => r.Key, r => r.Count)),
            NewUsersLast30Days = newUsers,
            WorkspacesByStatus = ByName(workspaces.ToDictionary(r => r.Key, r => r.Count)),
            SubmissionsByStatus = ByName(submissions.ToDictionary(r => r.Key, r => r.Count)),
            SubmissionsStuckAnalyzing = stuck,
            DriftAlertsByStatus = ByName(alerts.ToDictionary(r => r.Key, r => r.Count)),
            AuditRecordsByType = GovernanceAuditTypes.ToDictionary(t => t, t => audit.FirstOrDefault(a => a.Key == t)?.Count ?? 0)
        };
    }

    // Every value of the enum appears, with 0 where there is none, so the screen always shows the same rows.
    private static Dictionary<string, int> ByName<TEnum>(Dictionary<TEnum, int> counts) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().ToDictionary(v => v.ToString(), v => counts.GetValueOrDefault(v));
}
