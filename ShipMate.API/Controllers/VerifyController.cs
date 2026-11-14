using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Verify;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/verify")]
[Authorize]
public class VerifyController : ControllerBase
{
    private readonly IVerificationService _verificationService;
    private readonly ICurrentUserService _currentUserService;

    public VerifyController(IVerificationService verificationService, ICurrentUserService currentUserService)
    {
        _verificationService = verificationService;
        _currentUserService = currentUserService;
    }

    [HttpGet("checklists")]
    public async Task<IActionResult> GetAll(Guid workspaceId)
    {
        var result = await _verificationService.GetAllAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpGet("checklists/{version:int}")]
    public async Task<IActionResult> Get(Guid workspaceId, int version)
    {
        var result = await _verificationService.GetAsync(_currentUserService.UserId!.Value, workspaceId, version);
        return Ok(result);
    }

    [HttpPut("checklists/{version:int}/items/{itemKey}")]
    public async Task<IActionResult> CheckItem(
        Guid workspaceId, int version, string itemKey, CheckVerificationItemRequest request)
    {
        var result = await _verificationService.CheckItemAsync(
            _currentUserService.UserId!.Value, workspaceId, version, itemKey, request);
        return Ok(result);
    }

    [HttpPut("checklists/{version:int}/items/{itemKey}/applicability")]
    public async Task<IActionResult> SetApplicability(
        Guid workspaceId, int version, string itemKey, SetVerificationItemApplicabilityRequest request)
    {
        var result = await _verificationService.SetApplicabilityAsync(
            _currentUserService.UserId!.Value, workspaceId, version, itemKey, request);
        return Ok(result);
    }
}
