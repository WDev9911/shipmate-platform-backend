using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

/// <summary>What the administrators did, how the AI is used, and the counts of the platform. Numbers only, never workspace content.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Administrator")]
public class AdminReportsController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAdminReportsService _reportsService;

    public AdminReportsController(IAdminService adminService, IAdminReportsService reportsService)
    {
        _adminService = adminService;
        _reportsService = reportsService;
    }

    [HttpGet("action-logs")]
    public async Task<IActionResult> GetActionLogs([FromQuery] AdminActionLogQuery query)
    {
        var result = await _adminService.GetActionLogsAsync(query);
        return Ok(result);
    }

    [HttpGet("ai/usage")]
    public async Task<IActionResult> GetAiUsage([FromQuery] AiUsageQuery query) =>
        Ok(await _reportsService.GetAiUsageAsync(query));

    [HttpGet("monitoring/overview")]
    public async Task<IActionResult> GetOverview() => Ok(await _reportsService.GetOverviewAsync());
}
