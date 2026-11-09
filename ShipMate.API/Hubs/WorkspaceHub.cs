using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShipMate.Application.Interfaces.Repositories;
using ShipMate.Domain.Enums;

namespace ShipMate.API.Hubs;

/// <summary>
/// One connection per browser tab or CLI. Every connection is put in a group of its own user at once, and joins the
/// group of a workspace by asking, which only active members of it may do.
/// </summary>
[Authorize]
public class WorkspaceHub : Hub
{
    private readonly IWorkspaceMemberRepository _members;

    public WorkspaceHub(IWorkspaceMemberRepository members)
    {
        _members = members;
    }

    public static string WorkspaceGroup(Guid workspaceId) => $"workspace:{workspaceId}";

    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(CurrentUserId()));
        await base.OnConnectedAsync();
    }

    public async Task JoinWorkspace(Guid workspaceId)
    {
        var membership = await _members.GetByWorkspaceAndUserIdAsync(workspaceId, CurrentUserId());
        if (membership is null || membership.Status != WorkspaceMemberStatus.Active)
        {
            // The same answer as for a workspace that doesn't exist, so a stranger can't tell the two apart.
            throw new HubException("WORKSPACE_NOT_FOUND");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, WorkspaceGroup(workspaceId));
    }

    public Task LeaveWorkspace(Guid workspaceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, WorkspaceGroup(workspaceId));

    private Guid CurrentUserId()
    {
        var value = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var id) ? id : throw new HubException("UNAUTHORIZED");
    }
}
