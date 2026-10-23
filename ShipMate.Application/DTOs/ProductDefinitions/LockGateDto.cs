using ShipMate.Domain.Policies;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class LockGateDto
{
    public bool IsPassed { get; set; }
    public bool NoIncludedFeatures { get; set; }
    public int MembersWithoutSkill { get; set; }
    public List<FeatureReferenceDto> UnscoredFeatures { get; set; } = new();
    public List<ComplexityViolationDto> SkillCeilingViolations { get; set; } = new();
    public int TotalComplexity { get; set; }
    public int Budget { get; set; }
    public bool BudgetExceeded { get; set; }
    public List<BrokenDependencyDto> BrokenDependencies { get; set; } = new();

    public static LockGateDto From(LockGateResult result) => new()
    {
        IsPassed = result.IsPassed,
        NoIncludedFeatures = result.NoIncludedFeatures,
        MembersWithoutSkill = result.MembersWithoutSkill,
        UnscoredFeatures = result.UnscoredFeatures.Select(f => new FeatureReferenceDto(f.Id, f.Name)).ToList(),
        SkillCeilingViolations = result.SkillCeilingViolations.Select(ComplexityViolationDto.From).ToList(),
        TotalComplexity = result.TotalComplexity,
        Budget = result.Budget,
        BudgetExceeded = result.BudgetExceeded,
        BrokenDependencies = result.BrokenDependencies
            .Select(b => new BrokenDependencyDto(b.Feature.Id, b.Feature.Name, b.MissingFeatureId))
            .ToList()
    };
}

// IsCommitted tells the client not to suggest moving this feature to the Kill List: a committed feature
// can only be dropped through the Manager's confirmation.
public record ComplexityViolationDto(Guid FeatureId, string Name, int Complexity, bool IsCommitted)
{
    public static ComplexityViolationDto From(GateFeature feature) =>
        new(feature.Id, feature.Name, feature.Complexity!.Value, feature.IsCommitted);
}

public record BrokenDependencyDto(Guid FeatureId, string FeatureName, Guid MissingFeatureId);
