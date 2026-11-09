using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/drift-threshold")]
[Authorize]
public class DriftThresholdController : ControllerBase
{
    private readonly IDriftThresholdService _driftThresholdService;
    private readonly ICurrentUserService _currentUserService;

    public DriftThresholdController(IDriftThresholdService driftThresholdService, ICurrentUserService currentUserService)
    {
        _driftThresholdService = driftThresholdService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid workspaceId)
    {
        var result = await _driftThresholdService.GetAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> Change(Guid workspaceId, ChangeDriftThresholdRequest request)
    {
        var result = await _driftThresholdService.ChangeAsync(_currentUserService.UserId!.Value, workspaceId, request);
        return Ok(result);
    }
}
