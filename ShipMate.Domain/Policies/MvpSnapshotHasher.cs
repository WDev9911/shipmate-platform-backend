using System.Security.Cryptography;
using System.Text;
using ShipMate.Domain.Entities;

namespace ShipMate.Domain.Policies;

/// <summary>SHA-256 over a canonical, order-independent rendering of what a snapshot seals.</summary>
public static class MvpSnapshotHasher
{
    public static string Compute(MvpSnapshot snapshot)
    {
        var lines = new List<string> { $"version:{snapshot.Version}", $"dev_count:{snapshot.DevCountAtLock}" };

        lines.AddRange(snapshot.Features.OrderBy(f => f.FeatureId).Select(f =>
            $"feature:{f.FeatureId}|{f.Name}|{f.Description}|{f.Scope}|{f.Role}|{f.Origin}|{f.Complexity}|{f.Status}"));
        lines.AddRange(snapshot.Dependencies
            .OrderBy(d => d.FeatureId).ThenBy(d => d.DependsOnFeatureId)
            .Select(d => $"dependency:{d.FeatureId}->{d.DependsOnFeatureId}"));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
