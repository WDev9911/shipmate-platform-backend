using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/setup")]
[Authorize]
public class WorkspaceSetupController : ControllerBase
{
    private readonly IWorkspaceSetupService _workspaceSetupService;
    private readonly ICurrentUserService _currentUserService;

    public WorkspaceSetupController(IWorkspaceSetupService workspaceSetupService, ICurrentUserService currentUserService)
    {
        _workspaceSetupService = workspaceSetupService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid workspaceId)
    {
        var result = await _workspaceSetupService.GetAsync(_currentUserService.UserId!.Value, workspaceId);
        return Ok(result);
    }
}
