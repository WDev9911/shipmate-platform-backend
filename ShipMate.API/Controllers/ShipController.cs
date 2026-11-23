using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Ship;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/ship")]
[Authorize]
public class ShipController : ControllerBase
{
    private readonly IShipService _shipService;
    private readonly ICurrentUserService _currentUserService;

    public ShipController(IShipService shipService, ICurrentUserService currentUserService)
    {
        _shipService = shipService;
        _currentUserService = currentUserService;
    }

    [HttpGet("readiness")]
    public async Task<IActionResult> GetReadiness(Guid workspaceId)
    {
        var result = await _shipService.GetReadinessAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpGet("ownership-token")]
    public async Task<IActionResult> GetOwnership(Guid workspaceId)
    {
        var result = await _shipService.GetOwnershipAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    // 200 whether the checks passed or not: a failed check is a result to show, not a bad request.
    [HttpPost("attempts")]
    public async Task<IActionResult> StartAttempt(Guid workspaceId, StartShipAttemptRequest request)
    {
        var result = await _shipService.StartAttemptAsync(
            _currentUserService.UserId!.Value, workspaceId, request, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost("attempts/{attemptId:guid}/recheck")]
    public async Task<IActionResult> Recheck(Guid workspaceId, Guid attemptId)
    {
        var result = await _shipService.RecheckAsync(
            _currentUserService.UserId!.Value, workspaceId, attemptId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("attempts")]
    public async Task<IActionResult> GetAttempts(Guid workspaceId)
    {
        var result = await _shipService.GetAttemptsAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpGet("cards")]
    public async Task<IActionResult> GetCards(Guid workspaceId)
    {
        var result = await _shipService.GetCardsAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpGet("cards/{cardId:guid}")]
    public async Task<IActionResult> GetCard(Guid workspaceId, Guid cardId)
    {
        var result = await _shipService.GetCardAsync(_currentUserService.UserId!.Value, workspaceId, cardId);
        return Ok(result);
    }

    [HttpGet("cards/{cardId:guid}/verify")]
    public async Task<IActionResult> VerifyCard(Guid workspaceId, Guid cardId)
    {
        var result = await _shipService.VerifyCardAsync(_currentUserService.UserId!.Value, workspaceId, cardId);
        return Ok(result);
    }

    [HttpPut("cards/{cardId:guid}/visibility")]
    public async Task<IActionResult> SetVisibility(Guid workspaceId, Guid cardId, SetShipCardVisibilityRequest request)
    {
        var result = await _shipService.SetVisibilityAsync(_currentUserService.UserId!.Value, workspaceId, cardId, request);
        return Ok(result);
    }
}
