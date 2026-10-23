using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/assignments")]
[Authorize]
public class FeatureAssignmentController : ControllerBase
{
    private readonly IFeatureAssignmentService _featureAssignmentService;
    private readonly ICurrentUserService _currentUserService;

    public FeatureAssignmentController(
        IFeatureAssignmentService featureAssignmentService,
        ICurrentUserService currentUserService)
    {
        _featureAssignmentService = featureAssignmentService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetOverview(Guid workspaceId)
    {
        var result = await _featureAssignmentService.GetOverviewAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpPost("{featureId:guid}")]
    public async Task<IActionResult> Claim(Guid workspaceId, Guid featureId)
    {
        var result = await _featureAssignmentService.ClaimAsync(_currentUserService.UserId!.Value, workspaceId, featureId);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{featureId:guid}/branch")]
    public async Task<IActionResult> RetryBranch(Guid workspaceId, Guid featureId)
    {
        var result = await _featureAssignmentService.RetryBranchAsync(_currentUserService.UserId!.Value, workspaceId, featureId);
        return Ok(result);
    }
}
