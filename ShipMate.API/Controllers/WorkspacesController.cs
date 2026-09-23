using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipMate.Application.DTOs.Workspaces;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Controllers;

[ApiController]
[Route("api/workspaces")]
[Authorize]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ICurrentUserService _currentUserService;

    public WorkspacesController(IWorkspaceService workspaceService, ICurrentUserService currentUserService)
    {
        _workspaceService = workspaceService;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkspaceRequest request)
    {
        var result = await _workspaceService.CreateAsync(_currentUserService.UserId!.Value, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var result = await _workspaceService.GetMyWorkspacesAsync(_currentUserService.UserId!.Value);
        return Ok(result);
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetMyInvitations()
    {
        var result = await _workspaceService.GetMyInvitationsAsync(_currentUserService.UserId!.Value);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _workspaceService.GetByIdAsync(_currentUserService.UserId!.Value, id);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateWorkspaceRequest request)
    {
        var result = await _workspaceService.UpdateAsync(_currentUserService.UserId!.Value, id, request);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(Guid id)
    {
        await _workspaceService.ArchiveAsync(_currentUserService.UserId!.Value, id);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var result = await _workspaceService.GetMembersAsync(_currentUserService.UserId!.Value, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request)
    {
        var result = await _workspaceService.AddMemberAsync(_currentUserService.UserId!.Value, id, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, UpdateMemberRoleRequest request)
    {
        var result = await _workspaceService.UpdateMemberRoleAsync(_currentUserService.UserId!.Value, id, userId, request);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await _workspaceService.RemoveMemberAsync(_currentUserService.UserId!.Value, id, userId);
        return NoContent();
    }

    [HttpPost("{id:guid}/members/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid id)
    {
        var result = await _workspaceService.AcceptInvitationAsync(_currentUserService.UserId!.Value, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/members/decline")]
    public async Task<IActionResult> DeclineInvitation(Guid id)
    {
        await _workspaceService.DeclineInvitationAsync(_currentUserService.UserId!.Value, id);
        return NoContent();
    }

    [HttpGet("{id:guid}/github/repos")]
    public async Task<IActionResult> GetAvailableGitHubRepos(Guid id)
    {
        var result = await _workspaceService.GetAvailableGitHubReposAsync(_currentUserService.UserId!.Value, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/github/link")]
    public async Task<IActionResult> LinkGitHubRepo(Guid id, LinkGitHubRepoRequest request)
    {
        var result = await _workspaceService.LinkGitHubRepoAsync(_currentUserService.UserId!.Value, id, request);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/github")]
    public async Task<IActionResult> UnlinkGitHubRepo(Guid id)
    {
        await _workspaceService.UnlinkGitHubRepoAsync(_currentUserService.UserId!.Value, id);
        return NoContent();
    }
}
