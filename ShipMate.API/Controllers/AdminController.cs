using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Admin;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Administrator")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ICurrentUserService _currentUserService;

    public AdminController(IAdminService adminService, ICurrentUserService currentUserService)
    {
        _adminService = adminService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminService.GetUsersAsync(page, pageSize);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateUserStatusRequest request)
    {
        var result = await _adminService.UpdateUserStatusAsync(_currentUserService.UserId!.Value, id, request.Status);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, UpdateUserRoleRequest request)
    {
        var result = await _adminService.UpdateUserRoleAsync(_currentUserService.UserId!.Value, id, request.Role);
        return Ok(result);
    }

    [HttpGet("{id:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid id)
    {
        var result = await _adminService.GetUserActivityAsync(id);
        return Ok(result);
    }
}
