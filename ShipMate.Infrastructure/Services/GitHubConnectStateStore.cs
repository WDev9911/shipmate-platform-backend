using Microsoft.Extensions.Caching.Memory;
using ShipMate.Application.Interfaces.Services;

namespace ShipMate.Infrastructure.Services;

public class GitHubConnectStateStore : IGitHubConnectStateStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;

    public GitHubConnectStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Create(string state, Guid userId) =>
        _cache.Set(CacheKey(state), userId, Ttl);

    public Guid? Consume(string state)
    {
        var key = CacheKey(state);
        if (_cache.TryGetValue(key, out Guid userId))
        {
            _cache.Remove(key);
            return userId;
        }

        return null;
    }

    private static string CacheKey(string state) => $"github-connect-state:{state}";
}
