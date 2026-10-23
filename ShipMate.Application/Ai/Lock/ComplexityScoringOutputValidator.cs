using ShipMate.Domain.Constants;

namespace ShipMate.Application.Ai.Lock;

/// <summary>Checks what the JSON Schema can't: every requested feature scored exactly once, scores inside the scale.</summary>
public static class ComplexityScoringOutputValidator
{
    public static IReadOnlyList<string> Validate(ComplexityScoringOutput output, IReadOnlyCollection<string> featureIds)
    {
        var errors = new List<string>();
        var expected = new HashSet<string>(featureIds, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var score in output.Scores)
        {
            if (!expected.Contains(score.FeatureId))
            {
                errors.Add($"Score for unknown feature '{score.FeatureId}'.");
            }
            else if (!seen.Add(score.FeatureId))
            {
                errors.Add($"Feature '{score.FeatureId}' is scored more than once.");
            }

            if (!ComplexityScale.IsValid(score.Complexity))
            {
                errors.Add($"Complexity {score.Complexity} of feature '{score.FeatureId}' is outside {ComplexityScale.Min}-{ComplexityScale.Max}.");
            }
        }

        errors.AddRange(expected.Except(seen, StringComparer.OrdinalIgnoreCase)
            .Select(id => $"Feature '{id}' was not scored."));

        return errors;
    }
}
