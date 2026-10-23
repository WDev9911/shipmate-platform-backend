using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/product-definition/complexity")]
[Authorize]
public class FeatureComplexityController : ControllerBase
{
    private readonly IFeatureComplexityService _featureComplexityService;
    private readonly ICurrentUserService _currentUserService;

    public FeatureComplexityController(
        IFeatureComplexityService featureComplexityService,
        ICurrentUserService currentUserService)
    {
        _featureComplexityService = featureComplexityService;
        _currentUserService = currentUserService;
    }

    [HttpPost("propose")]
    public async Task<IActionResult> Propose(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = await _featureComplexityService.ProposeAsync(
            _currentUserService.UserId!.Value, workspaceId, cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> Set(Guid workspaceId, SetComplexityRequest request)
    {
        var result = await _featureComplexityService.SetAsync(_currentUserService.UserId!.Value, workspaceId, request);
        return Ok(result);
    }
}
