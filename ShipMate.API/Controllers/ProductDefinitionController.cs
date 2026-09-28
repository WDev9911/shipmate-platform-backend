using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/product-definition")]
[Authorize]
public class ProductDefinitionController : ControllerBase
{
    private readonly IProductDefinitionService _productDefinitionService;
    private readonly ICurrentUserService _currentUserService;

    public ProductDefinitionController(
        IProductDefinitionService productDefinitionService,
        ICurrentUserService currentUserService)
    {
        _productDefinitionService = productDefinitionService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid workspaceId)
    {
        var result = await _productDefinitionService.GetAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpPost("committed-features")]
    public async Task<IActionResult> AddCommittedFeature(Guid workspaceId, CreateCommittedFeatureRequest request)
    {
        var result = await _productDefinitionService.AddCommittedFeatureAsync(
            _currentUserService.UserId!.Value, workspaceId, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("committed-features/{featureId:guid}")]
    public async Task<IActionResult> UpdateCommittedFeature(
        Guid workspaceId, Guid featureId, UpdateCommittedFeatureRequest request)
    {
        var result = await _productDefinitionService.UpdateCommittedFeatureAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId, request);
        return Ok(result);
    }

    [HttpDelete("committed-features/{featureId:guid}")]
    public async Task<IActionResult> DeleteCommittedFeature(Guid workspaceId, Guid featureId)
    {
        await _productDefinitionService.DeleteCommittedFeatureAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId);
        return NoContent();
    }
}
