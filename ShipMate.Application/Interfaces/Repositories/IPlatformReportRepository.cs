using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Interfaces.Repositories;

/// <summary>Counts across the whole platform for the administrator, done in SQL; no record is loaded.</summary>
public interface IPlatformReportRepository
{
    Task<List<AiUsageRowDto>> GetAiUsageAsync(DateTime fromUtc, DateTime toUtc);
    Task<PlatformOverviewDto> GetOverviewAsync(DateTime nowUtc);
}
