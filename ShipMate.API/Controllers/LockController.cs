using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}")]
[Authorize]
public class LockController : ControllerBase
{
    private readonly ILockService _lockService;
    private readonly ICurrentUserService _currentUserService;

    public LockController(ILockService lockService, ICurrentUserService currentUserService)
    {
        _lockService = lockService;
        _currentUserService = currentUserService;
    }

    [HttpGet("lock/check")]
    public async Task<IActionResult> Check(Guid workspaceId)
    {
        var result = await _lockService.CheckAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpPost("lock")]
    public async Task<IActionResult> Lock(Guid workspaceId)
    {
        var result = await _lockService.LockAsync(_currentUserService.UserId!.Value, workspaceId);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetLatestSnapshot(Guid workspaceId)
    {
        var result = await _lockService.GetLatestSnapshotAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }
}
