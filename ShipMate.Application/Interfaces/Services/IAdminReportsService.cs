using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Interfaces.Services;

public interface IAdminReportsService
{
    Task<AiUsageDto> GetAiUsageAsync(AiUsageQuery query);
    Task<PlatformOverviewDto> GetOverviewAsync();
}
