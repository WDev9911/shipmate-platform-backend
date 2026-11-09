using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Build;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Domain.Enums;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/build")]
[Authorize]
public class BuildController : ControllerBase
{
    private readonly IBuildService _buildService;
    private readonly IBuildApprovalService _buildApprovalService;
    private readonly IDriftAlertService _driftAlertService;
    private readonly ICurrentUserService _currentUserService;

    public BuildController(
        IBuildService buildService, IBuildApprovalService buildApprovalService, IDriftAlertService driftAlertService,
        ICurrentUserService currentUserService)
    {
        _buildService = buildService;
        _buildApprovalService = buildApprovalService;
        _driftAlertService = driftAlertService;
        _currentUserService = currentUserService;
    }

    [HttpPost("submissions")]
    public async Task<IActionResult> Submit(Guid workspaceId, SubmitBuildRequest request)
    {
        var result = await _buildService.SubmitAsync(_currentUserService.UserId!.Value, workspaceId, request);
        return StatusCode(StatusCodes.Status202Accepted, result);
    }

    [HttpGet("submissions/{submissionId:guid}")]
    public async Task<IActionResult> GetSubmission(Guid workspaceId, Guid submissionId)
    {
        var result = await _buildService.GetSubmissionAsync(_currentUserService.UserId!.Value, workspaceId, submissionId);
        return Ok(result);
    }

    [HttpPost("submissions/{submissionId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid workspaceId, Guid submissionId, ApproveBuildRequest? request = null)
    {
        var result = await _buildApprovalService.ApproveAsync(
            _currentUserService.UserId!.Value, workspaceId, submissionId, request ?? new ApproveBuildRequest());
        return Ok(result);
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts(Guid workspaceId, [FromQuery] DriftAlertStatus? status)
    {
        var result = await _driftAlertService.GetAllAsync(_currentUserService.UserId!.Value, workspaceId, status);
        return Ok(result);
    }

    [HttpPost("alerts/{alertId:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid workspaceId, Guid alertId)
    {
        var result = await _driftAlertService.AcknowledgeAsync(_currentUserService.UserId!.Value, workspaceId, alertId);
        return Ok(result);
    }

    [HttpPost("alerts/{alertId:guid}/override")]
    public async Task<IActionResult> Override(Guid workspaceId, Guid alertId, OverrideDriftAlertRequest request)
    {
        var result = await _driftAlertService.OverrideAsync(_currentUserService.UserId!.Value, workspaceId, alertId, request);
        return Ok(result);
    }
}
