using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

/// <summary>What the administrators did, and later the counts of the platform. Numbers only, never workspace content.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Administrator")]
public class AdminReportsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminReportsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("action-logs")]
    public async Task<IActionResult> GetActionLogs([FromQuery] AdminActionLogQuery query)
    {
        var result = await _adminService.GetActionLogsAsync(query);
        return Ok(result);
    }
}
