using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/next-best-action")]
[Authorize]
public class NextBestActionController : ControllerBase
{
    private readonly INextBestActionService _nextBestActionService;
    private readonly ICurrentUserService _currentUserService;

    public NextBestActionController(INextBestActionService nextBestActionService, ICurrentUserService currentUserService)
    {
        _nextBestActionService = nextBestActionService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid workspaceId)
    {
        var result = await _nextBestActionService.GetAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }
}
