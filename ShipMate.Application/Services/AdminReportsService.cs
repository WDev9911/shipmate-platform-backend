using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Application.Services;

public class AdminReportsService : IAdminReportsService
{
    public static readonly TimeSpan DefaultRange = TimeSpan.FromDays(30);

    private readonly IPlatformReportRepository _reports;

    public AdminReportsService(IPlatformReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<AiUsageDto> GetAiUsageAsync(AiUsageQuery query)
    {
        var (from, to) = Range(query, DateTime.UtcNow);
        return new AiUsageDto { From = from, To = to, Rows = await _reports.GetAiUsageAsync(from, to) };
    }

    public Task<PlatformOverviewDto> GetOverviewAsync() => _reports.GetOverviewAsync(DateTime.UtcNow);

    /// <summary>
    /// The range asked for, in UTC. A date from the query string has no time zone; it is read as UTC, the zone the
    /// database stores, rather than as the server's local time.
    /// </summary>
    public static (DateTime From, DateTime To) Range(AiUsageQuery query, DateTime nowUtc)
    {
        var to = query.To is { } t ? AsUtc(t) : nowUtc;
        var from = query.From is { } f ? AsUtc(f) : to - DefaultRange;
        return (from, to);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
