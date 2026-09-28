using ShipMate.Domain.Entities;

namespace ShipMate.Domain.Policies;

/// <summary>
/// The depends_on graph of a product definition. DEFINE step 7 requires it to be acyclic before READY_FOR_LOCK.
/// </summary>
public static class FeatureDependencyGraph
{
    /// <summary>
    /// The cycle that adding "feature depends on prerequisite" would close, as feature ids in dependency order
    /// starting and ending with <paramref name="featureId"/>; empty when the new link is safe.
    /// </summary>
    public static IReadOnlyList<Guid> FindCycleIfLinked(IEnumerable<Feature> features, Guid featureId, Guid prerequisiteId)
    {
        var prerequisitesById = features.ToDictionary(
            f => f.Id,
            f => f.Dependencies.Select(d => d.DependsOnFeatureId).ToList());

        // The new link closes a cycle exactly when the feature is already reachable from the prerequisite.
        var path = FindPath(prerequisitesById, prerequisiteId, featureId);
        return path.Count == 0 ? [] : [featureId, .. path];
    }

    private static List<Guid> FindPath(IReadOnlyDictionary<Guid, List<Guid>> prerequisitesById, Guid from, Guid to)
    {
        var previous = new Dictionary<Guid, Guid>();
        var visited = new HashSet<Guid> { from };
        var queue = new Queue<Guid>([from]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to)
            {
                var path = new List<Guid> { current };
                while (previous.TryGetValue(current, out var step))
                {
                    path.Insert(0, step);
                    current = step;
                }

                return path;
            }

            foreach (var next in prerequisitesById.GetValueOrDefault(current, []).Where(visited.Add))
            {
                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        return [];
    }
}
