using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace ShipMate.API.Hubs;

/// <summary>
/// Remembers who is connected, so a locked account can be cut off at once. In memory, so it is right for a single
/// instance only, the same assumption the rest of the SignalR setup makes.
/// </summary>
public class HubConnectionRegistry
{
    private readonly ConcurrentDictionary<string, (Guid UserId, HubCallerContext Context)> _connections = new();

    public void Add(Guid userId, HubCallerContext context) =>
        _connections[context.ConnectionId] = (userId, context);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    /// <summary>Aborts every connection of the user; each one then removes itself when its disconnect arrives.</summary>
    public void AbortAll(Guid userId)
    {
        foreach (var (_, context) in _connections.Values.Where(c => c.UserId == userId))
        {
            context.Abort();
        }
    }
}
