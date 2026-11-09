namespace ShipMate.Application.Ai.Build;

/// <summary>Checks what the JSON Schema can't: the score is a probability, and the AI only names features that exist.</summary>
public static class BuildAnalysisOutputValidator
{
    /// <param name="otherFeatureIds">Every feature of the snapshot except the one being built: the only ids the AI may suspect.</param>
    public static IReadOnlyList<string> Validate(BuildAnalysisOutput output, IReadOnlyCollection<string> otherFeatureIds)
    {
        var errors = new List<string>();
        var known = new HashSet<string>(otherFeatureIds, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (output.ConfidenceScore is < 0m or > 1m)
        {
            errors.Add($"The confidence score {output.ConfidenceScore} is outside 0-1.");
        }

        foreach (var id in output.SuspectedFeatureIds)
        {
            if (!known.Contains(id))
            {
                errors.Add($"Suspected feature '{id}' is not another feature of the snapshot.");
            }
            else if (!seen.Add(id))
            {
                errors.Add($"Suspected feature '{id}' is listed more than once.");
            }
        }

        if (output.Findings.Any(f => string.IsNullOrWhiteSpace(f.Description)))
        {
            errors.Add("A finding has no description.");
        }

        if (output.Suggestions.Any(s => string.IsNullOrWhiteSpace(s.Suggestion) || string.IsNullOrWhiteSpace(s.Reason)))
        {
            errors.Add("A suggestion has no suggestion or reason.");
        }

        return errors;
    }
}
