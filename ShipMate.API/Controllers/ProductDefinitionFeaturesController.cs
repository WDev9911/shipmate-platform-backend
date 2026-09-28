using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/product-definition/features")]
[Authorize]
public class ProductDefinitionFeaturesController : ControllerBase
{
    private readonly IFeatureReviewService _featureReviewService;
    private readonly ICurrentUserService _currentUserService;

    public ProductDefinitionFeaturesController(
        IFeatureReviewService featureReviewService,
        ICurrentUserService currentUserService)
    {
        _featureReviewService = featureReviewService;
        _currentUserService = currentUserService;
    }

    [HttpPatch("{featureId:guid}")]
    public async Task<IActionResult> Edit(Guid workspaceId, Guid featureId, UpdateFeatureRequest request)
    {
        var result = await _featureReviewService.EditAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId, request);
        return Ok(result);
    }

    [HttpPatch("{featureId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid workspaceId, Guid featureId, UpdateFeatureStatusRequest request)
    {
        var result = await _featureReviewService.ChangeStatusAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId, request);
        return Ok(result);
    }

    [HttpPost("{featureId:guid}/merge")]
    public async Task<IActionResult> Merge(Guid workspaceId, Guid featureId)
    {
        var result = await _featureReviewService.MergeAsync(_currentUserService.UserId!.Value, workspaceId, featureId);
        return Ok(result);
    }

    [HttpPost("{featureId:guid}/keep-separate")]
    public async Task<IActionResult> KeepSeparate(Guid workspaceId, Guid featureId)
    {
        var result = await _featureReviewService.KeepSeparateAsync(_currentUserService.UserId!.Value, workspaceId, featureId);
        return Ok(result);
    }

    [HttpPost("{featureId:guid}/dependencies")]
    public async Task<IActionResult> AddDependency(Guid workspaceId, Guid featureId, AddFeatureDependencyRequest request)
    {
        var result = await _featureReviewService.AddDependencyAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{featureId:guid}/dependencies/{dependsOnFeatureId:guid}")]
    public async Task<IActionResult> RemoveDependency(Guid workspaceId, Guid featureId, Guid dependsOnFeatureId)
    {
        await _featureReviewService.RemoveDependencyAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId, dependsOnFeatureId);
        return NoContent();
    }

    [HttpPost("{featureId:guid}/accept-persona-conflict")]
    public async Task<IActionResult> AcceptPersonaConflict(Guid workspaceId, Guid featureId)
    {
        var result = await _featureReviewService.AcceptPersonaConflictAsync(
            _currentUserService.UserId!.Value, workspaceId, featureId);
        return Ok(result);
    }
}
