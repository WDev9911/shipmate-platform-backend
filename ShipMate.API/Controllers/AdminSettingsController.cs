using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

/// <summary>The platform settings: what a new workspace starts with, and the words the BUILD Rule Engine counts as maintenance.</summary>
[ApiController]
[Route("api/admin/settings")]
[Authorize(Roles = "Administrator")]
public class AdminSettingsController : ControllerBase
{
    private readonly IPlatformSettingsService _settingsService;
    private readonly ICurrentUserService _currentUserService;

    public AdminSettingsController(IPlatformSettingsService settingsService, ICurrentUserService currentUserService)
    {
        _settingsService = settingsService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _settingsService.GetAsync());

    [HttpPut]
    public async Task<IActionResult> Update(UpdatePlatformSettingsRequest request) =>
        Ok(await _settingsService.UpdateAsync(_currentUserService.UserId!.Value, request));
}
