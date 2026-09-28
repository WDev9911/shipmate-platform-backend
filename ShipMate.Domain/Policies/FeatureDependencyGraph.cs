using ShipMate.Domain.Entities;

namespace ShipMate.Domain.Policies;

/// <summary>
/// The depends_on graph of a product definition. DEFINE step 7 requires it to be acyclic before READY_FOR_LOCK.
/// Cycles are returned as feature ids in dependency order, starting and ending with the same feature.
/// </summary>
public static class FeatureDependencyGraph
{
    /// <summary>The cycle that adding "feature depends on prerequisite" would close; empty when the new link is safe.</summary>
    public static IReadOnlyList<Guid> FindCycleIfLinked(IEnumerable<Feature> features, Guid featureId, Guid prerequisiteId)
    {
        // The new link closes a cycle exactly when the feature is already reachable from the prerequisite.
        var path = FindPath(BuildPrerequisites(features), prerequisiteId, featureId);
        return path.Count == 0 ? [] : [featureId, .. path];
    }

    /// <summary>One cycle in the whole graph, or empty when there is none.</summary>
    public static IReadOnlyList<Guid> FindCycle(IEnumerable<Feature> features)
    {
        var prerequisitesById = BuildPrerequisites(features);
        var finished = new HashSet<Guid>();
        var onPath = new List<Guid>();

        foreach (var featureId in prerequisitesById.Keys)
        {
            var cycle = Visit(featureId);
            if (cycle.Count > 0)
            {
                return cycle;
            }
        }

        return [];

        List<Guid> Visit(Guid featureId)
        {
            if (finished.Contains(featureId))
            {
                return [];
            }

            var indexOnPath = onPath.IndexOf(featureId);
            if (indexOnPath >= 0)
            {
                return [.. onPath.Skip(indexOnPath), featureId];
            }

            onPath.Add(featureId);
            foreach (var prerequisiteId in prerequisitesById.GetValueOrDefault(featureId, []))
            {
                var cycle = Visit(prerequisiteId);
                if (cycle.Count > 0)
                {
                    return cycle;
                }
            }

            onPath.RemoveAt(onPath.Count - 1);
            finished.Add(featureId);
            return [];
        }
    }

    private static Dictionary<Guid, List<Guid>> BuildPrerequisites(IEnumerable<Feature> features) =>
        features.ToDictionary(
            f => f.Id,
            f => f.Dependencies.Select(d => d.DependsOnFeatureId).ToList());

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
