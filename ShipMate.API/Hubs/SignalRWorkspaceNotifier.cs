using Microsoft.AspNetCore.SignalR;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.API.Hubs;

public class SignalRWorkspaceNotifier : IWorkspaceNotifier
{
    private readonly IHubContext<WorkspaceHub> _hub;
    private readonly HubConnectionRegistry _registry;
    private readonly ILogger<SignalRWorkspaceNotifier> _logger;

    public SignalRWorkspaceNotifier(
        IHubContext<WorkspaceHub> hub, HubConnectionRegistry registry, ILogger<SignalRWorkspaceNotifier> logger)
    {
        _hub = hub;
        _registry = registry;
        _logger = logger;
    }

    public Task DisconnectUserAsync(Guid userId)
    {
        _registry.AbortAll(userId);
        return Task.CompletedTask;
    }

    public Task PublishAsync(Guid workspaceId, string eventName, object payload) =>
        SendAsync(_hub.Clients.Group(WorkspaceHub.WorkspaceGroup(workspaceId)), eventName, payload);

    public Task PublishToUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload)
    {
        var groups = userIds.Distinct().Select(WorkspaceHub.UserGroup).ToList();
        return groups.Count == 0 ? Task.CompletedTask : SendAsync(_hub.Clients.Groups(groups), eventName, payload);
    }

    // The work that caused the message is already saved; a client that misses it can still read the state over the API.
    private async Task SendAsync(IClientProxy clients, string eventName, object payload)
    {
        try
        {
            await clients.SendAsync(eventName, payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not deliver the {Event} message.", eventName);
        }
    }
}
