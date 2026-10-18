using ShipMate.Domain.Entities;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Policies;

/// <summary>
/// A feature as the Lock gate sees it, so the same checks run on the Product Definition (first Lock) and on a
/// snapshot being evolved (Feature Challenge). A backup feature counts toward the budget but not the Skill Ceiling.
/// </summary>
public record GateFeature(
    Guid Id,
    string Name,
    int? Complexity,
    bool IsCommitted,
    bool IsBackup,
    IReadOnlyList<Guid> DependsOn)
{
    public static GateFeature From(Feature feature) => new(
        feature.Id,
        feature.Name,
        feature.Complexity,
        feature.Origin == FeatureOrigin.FromCommittedList,
        IsBackup: false,
        feature.Dependencies.Select(d => d.DependsOnFeatureId).ToList());
}

public record BrokenDependency(GateFeature Feature, Guid MissingFeatureId);

/// <summary>What still blocks Lock; it may proceed when every list is empty and the budget is respected.</summary>
public record LockGateResult(
    bool NoIncludedFeatures,
    IReadOnlyList<GateFeature> UnscoredFeatures,
    int MembersWithoutSkill,
    IReadOnlyList<GateFeature> SkillCeilingViolations,
    int TotalComplexity,
    int Budget,
    IReadOnlyList<BrokenDependency> BrokenDependencies)
{
    public bool BudgetExceeded => TotalComplexity > Budget;

    public bool IsPassed =>
        !NoIncludedFeatures
        && UnscoredFeatures.Count == 0
        && MembersWithoutSkill == 0
        && SkillCeilingViolations.Count == 0
        && !BudgetExceeded
        && BrokenDependencies.Count == 0;
}

/// <summary>
/// LOCK step 2: three independent checks that must all pass — Skill Ceiling (each feature no harder than the
/// best active developer), Total Complexity Budget (sum of scores within 10 per active developer) and
/// Dependency Integrity (every depends_on points at a feature that is in the snapshot).
/// </summary>
public static class LockGatePolicy
{
    public const int BudgetPerDeveloper = 10;

    public static LockGateResult Evaluate(IReadOnlyList<Feature> includedFeatures, IReadOnlyList<int?> memberSkills) =>
        Evaluate(includedFeatures.Select(GateFeature.From).ToList(), memberSkills);

    /// <param name="features">The features about to be sealed.</param>
    /// <param name="memberSkills">The skill level of every active member; null where not declared yet.</param>
    public static LockGateResult Evaluate(IReadOnlyList<GateFeature> features, IReadOnlyList<int?> memberSkills)
    {
        var scored = features.Where(f => f.Complexity is not null).ToList();
        var declaredSkills = memberSkills.Where(s => s is not null).Select(s => s!.Value).ToList();

        // Without any declared skill there is no ceiling to compare against; MembersWithoutSkill already blocks.
        var skillCeiling = declaredSkills.Count == 0 ? (int?)null : declaredSkills.Max();
        var ceilingViolations = skillCeiling is null
            ? []
            : scored.Where(f => !f.IsBackup && f.Complexity > skillCeiling).ToList();

        var ids = features.Select(f => f.Id).ToHashSet();
        var brokenDependencies = features
            .SelectMany(f => f.DependsOn
                .Where(id => !ids.Contains(id))
                .Select(id => new BrokenDependency(f, id)))
            .ToList();

        return new LockGateResult(
            NoIncludedFeatures: features.Count == 0,
            UnscoredFeatures: features.Where(f => f.Complexity is null).ToList(),
            MembersWithoutSkill: memberSkills.Count(s => s is null),
            SkillCeilingViolations: ceilingViolations,
            TotalComplexity: scored.Sum(f => f.Complexity!.Value),
            Budget: BudgetPerDeveloper * memberSkills.Count,
            BrokenDependencies: brokenDependencies);
    }
}
