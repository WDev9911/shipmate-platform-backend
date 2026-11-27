using ShipMate.Application.DTOs.Admin;

namespace ShipMate.Application.Interfaces.Services;

public interface IPlatformSettingsService
{
    Task<PlatformSettingsDto> GetAsync();
    Task<PlatformSettingsDto> UpdateAsync(Guid adminUserId, UpdatePlatformSettingsRequest request);
}
