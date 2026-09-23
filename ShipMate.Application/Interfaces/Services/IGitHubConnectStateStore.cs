namespace ShipMate.Application.Interfaces.Services;

/// <summary>
/// Maps an OAuth `state` value to the authenticated user who requested it, so the shared
/// GitHub callback can tell a "connect to my existing account" request apart from a login request.
/// </summary>
public interface IGitHubConnectStateStore
{
    void Create(string state, Guid userId);
    Guid? Consume(string state);
}
