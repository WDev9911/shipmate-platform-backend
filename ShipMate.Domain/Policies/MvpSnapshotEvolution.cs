using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

public enum EvolutionError
{
    None,
    RemovedFeatureNotFound,
    DependentsResolutionRequired,
    ReplacementInvalid
}

/// <summary>
/// Builds the next snapshot version for a Feature Challenge: a full copy of the previous one plus the approved
/// change. Snapshots are never edited in place; the previous version is left untouched.
/// </summary>
public static class MvpSnapshotEvolution
{
    public static MvpSnapshot NextVersion(MvpSnapshot previous, Guid userId, int devCount)
    {
        var next = new MvpSnapshot
        {
            WorkspaceId = previous.WorkspaceId,
            Version = previous.Version + 1,
            DevCountAtLock = devCount,
            CreatedByUserId = userId
        };

        foreach (var feature in previous.Features)
        {
            next.Features.Add(new SnapshotFeature
            {
                FeatureId = feature.FeatureId,
                Name = feature.Name,
                Description = feature.Description,
                Scope = feature.Scope,
                Role = feature.Role,
                Origin = feature.Origin,
                Complexity = feature.Complexity,
                Status = feature.Status
            });
        }

        foreach (var dependency in previous.Dependencies)
        {
            next.Dependencies.Add(new SnapshotDependency
            {
                FeatureId = dependency.FeatureId,
                DependsOnFeatureId = dependency.DependsOnFeatureId
            });
        }

        return next;
    }

    /// <summary>The features that still count: locked and backup ones, with their dependencies.</summary>
    public static List<GateFeature> ActiveFeatures(MvpSnapshot snapshot) =>
        snapshot.Features
            .Where(IsActive)
            .Select(f => new GateFeature(
                f.FeatureId,
                f.Name,
                f.Complexity,
                f.Origin == FeatureOrigin.FromCommittedList,
                f.Status == SnapshotFeatureStatus.Backup,
                snapshot.Dependencies.Where(d => d.FeatureId == f.FeatureId).Select(d => d.DependsOnFeatureId).ToList()))
            .ToList();

    /// <summary>Every active feature that depends on the given one, directly or through other features.</summary>
    public static IReadOnlyList<Guid> DependentsOf(MvpSnapshot snapshot, Guid featureId)
    {
        var activeIds = snapshot.Features.Where(IsActive).Select(f => f.FeatureId).ToHashSet();
        var found = new List<Guid>();
        var visited = new HashSet<Guid> { featureId };
        var queue = new Queue<Guid>([featureId]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dependent in snapshot.Dependencies
                         .Where(d => d.DependsOnFeatureId == current && activeIds.Contains(d.FeatureId))
                         .Select(d => d.FeatureId)
                         .Where(visited.Add))
            {
                found.Add(dependent);
                queue.Enqueue(dependent);
            }
        }

        return found;
    }

    /// <summary>Adds the challenged feature as a new locked or backup feature. It is always from_ai.</summary>
    public static void AddFeature(MvpSnapshot next, FeatureChallenge challenge, SnapshotFeatureStatus status) =>
        next.Features.Add(new SnapshotFeature
        {
            FeatureId = challenge.FeatureId,
            Name = challenge.Name,
            Description = challenge.Description,
            Origin = FeatureOrigin.FromAi,
            Complexity = challenge.Complexity!.Value,
            Status = status
        });

    /// <summary>
    /// Replaces a feature with the challenged one. Features that depended on the removed one are either removed
    /// with it, or restructured to depend on a replacement (the new feature unless another one is chosen).
    /// </summary>
    public static EvolutionError RemoveAndReplace(
        MvpSnapshot next, FeatureChallenge challenge, Guid removedFeatureId,
        RemovalDependentsResolution? resolution, Guid? replacementFeatureId)
    {
        var removed = next.Features.FirstOrDefault(f => f.FeatureId == removedFeatureId && IsActive(f));
        if (removed is null)
        {
            return EvolutionError.RemovedFeatureNotFound;
        }

        var dependents = DependentsOf(next, removedFeatureId);
        if (dependents.Count > 0 && resolution is null)
        {
            return EvolutionError.DependentsResolutionRequired;
        }

        var replacementId = replacementFeatureId ?? challenge.FeatureId;
        if (dependents.Count > 0 && resolution == RemovalDependentsResolution.Restructure)
        {
            var isValid = replacementId == challenge.FeatureId
                || next.Features.Any(f => f.FeatureId == replacementId && IsActive(f))
                   && replacementId != removedFeatureId
                   && !dependents.Contains(replacementId);
            if (!isValid)
            {
                return EvolutionError.ReplacementInvalid;
            }
        }

        removed.Status = SnapshotFeatureStatus.Replaced;
        AddFeature(next, challenge, SnapshotFeatureStatus.Locked);

        if (dependents.Count > 0 && resolution == RemovalDependentsResolution.RemoveDependents)
        {
            foreach (var feature in next.Features.Where(f => dependents.Contains(f.FeatureId)))
            {
                feature.Status = SnapshotFeatureStatus.Replaced;
            }
        }
        else if (dependents.Count > 0)
        {
            Restructure(next, removedFeatureId, replacementId);
        }

        // A replaced feature keeps no links, and nothing keeps a link to it.
        var replacedIds = next.Features.Where(f => f.Status == SnapshotFeatureStatus.Replaced).Select(f => f.FeatureId).ToHashSet();
        foreach (var dependency in next.Dependencies
                     .Where(d => replacedIds.Contains(d.FeatureId) || replacedIds.Contains(d.DependsOnFeatureId))
                     .ToList())
        {
            next.Dependencies.Remove(dependency);
        }

        return EvolutionError.None;
    }

    // A replacement can't close a cycle: one that reached a dependent would itself depend on the removed feature,
    // and such features are rejected as replacements.
    private static void Restructure(MvpSnapshot next, Guid removedFeatureId, Guid replacementId)
    {
        var directDependents = next.Dependencies
            .Where(d => d.DependsOnFeatureId == removedFeatureId)
            .Select(d => d.FeatureId)
            .ToList();

        foreach (var dependentId in directDependents.Where(id => id != replacementId))
        {
            if (!next.Dependencies.Any(d => d.FeatureId == dependentId && d.DependsOnFeatureId == replacementId))
            {
                next.Dependencies.Add(new SnapshotDependency { FeatureId = dependentId, DependsOnFeatureId = replacementId });
            }
        }

    }

    private static bool IsActive(SnapshotFeature feature) => feature.Status != SnapshotFeatureStatus.Replaced;
}
