using Microsoft.Extensions.Caching.Memory;
using ShipMate.Application.DTOs.Auth;
using ShipMate.Application.Interfaces.Services;
using ShipMate.Application.Security;

namespace ShipMate.Infrastructure.Services;

public class OAuthHandoffStore : IOAuthHandoffStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    private readonly IMemoryCache _cache;

    public OAuthHandoffStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Create(AuthResponse response)
    {
        var (code, _) = SecureTokenGenerator.GenerateTokenPair();
        _cache.Set(CacheKey(code), response, Ttl);
        return code;
    }

    public AuthResponse? Consume(string handoffCode)
    {
        var key = CacheKey(handoffCode);
        if (_cache.TryGetValue(key, out AuthResponse? response))
        {
            _cache.Remove(key);
            return response;
        }

        return null;
    }

    private static string CacheKey(string code) => $"oauth-handoff:{code}";
}
