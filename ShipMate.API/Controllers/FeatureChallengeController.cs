using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.ProductDefinitions;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/challenges")]
[Authorize]
public class FeatureChallengeController : ControllerBase
{
    private readonly IFeatureChallengeService _featureChallengeService;
    private readonly ICurrentUserService _currentUserService;

    public FeatureChallengeController(
        IFeatureChallengeService featureChallengeService,
        ICurrentUserService currentUserService)
    {
        _featureChallengeService = featureChallengeService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid workspaceId)
    {
        var result = await _featureChallengeService.GetAllAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }

    [HttpGet("{challengeId:guid}")]
    public async Task<IActionResult> Get(Guid workspaceId, Guid challengeId)
    {
        var result = await _featureChallengeService.GetAsync(_currentUserService.UserId!.Value, workspaceId, challengeId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(Guid workspaceId, SubmitChallengeRequest request, CancellationToken cancellationToken)
    {
        var result = await _featureChallengeService.SubmitAsync(
            _currentUserService.UserId!.Value, workspaceId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{challengeId:guid}/complexity")]
    public async Task<IActionResult> ConfirmComplexity(
        Guid workspaceId, Guid challengeId, ConfirmChallengeComplexityRequest request)
    {
        var result = await _featureChallengeService.ConfirmComplexityAsync(
            _currentUserService.UserId!.Value, workspaceId, challengeId, request);
        return Ok(result);
    }

    [HttpPost("{challengeId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid workspaceId, Guid challengeId, ApproveChallengeRequest request)
    {
        var result = await _featureChallengeService.ApproveAsync(
            _currentUserService.UserId!.Value, workspaceId, challengeId, request);
        return Ok(result);
    }

    [HttpPost("{challengeId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid workspaceId, Guid challengeId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RejectChallengeRequest? request)
    {
        var result = await _featureChallengeService.RejectAsync(
            _currentUserService.UserId!.Value, workspaceId, challengeId, request ?? new RejectChallengeRequest());
        return Ok(result);
    }
}
