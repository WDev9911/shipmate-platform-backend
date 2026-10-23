using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>Action is null when no option can fit the new feature (even removing any single feature isn't enough).</summary>
public record ChallengeRecommendation(ChallengeAction? Action, GateFeature? FeatureToRemove);

/// <summary>
/// The suggestion shown to the Manager, who still decides. Backup only when the feature is harder than every
/// developer can handle; otherwise add it if the budget allows; otherwise replace one feature, preferring one
/// that is not committed and the least complex.
/// </summary>
public static class ChallengeRecommendationPolicy
{
    public static ChallengeRecommendation Recommend(
        IReadOnlyList<GateFeature> activeFeatures, int newComplexity, IReadOnlyList<int?> memberSkills)
    {
        var skillCeiling = memberSkills.Where(s => s is not null).Select(s => s!.Value).DefaultIfEmpty(0).Max();
        if (newComplexity > skillCeiling)
        {
            return new ChallengeRecommendation(ChallengeAction.Backup, null);
        }

        var budget = LockGatePolicy.BudgetPerDeveloper * memberSkills.Count;
        var total = activeFeatures.Sum(f => f.Complexity ?? 0);
        if (total + newComplexity <= budget)
        {
            return new ChallengeRecommendation(ChallengeAction.AddDirectly, null);
        }

        var candidate = activeFeatures
            .Where(f => total - (f.Complexity ?? 0) + newComplexity <= budget)
            .OrderBy(f => f.IsCommitted)
            .ThenBy(f => f.Complexity)
            .ThenBy(f => f.Name)
            .FirstOrDefault();

        return candidate is null
            ? new ChallengeRecommendation(null, null)
            : new ChallengeRecommendation(ChallengeAction.RemoveAndReplace, candidate);
    }
}
