using ShipMate.Domain.Constants;
using ShipMate.Domain.Enums;

namespace ShipMate.Application.Ai.Define;

/// <summary>
/// Checks the rules a DEFINE analysis must satisfy before anything is saved. The JSON Schema already
/// guarantees the shape; this covers what a schema can't express (ids, references, spec rules).
/// </summary>
public static class DefineAnalysisOutputValidator
{
    public static IReadOnlyList<string> Validate(DefineAnalysisOutput output, IReadOnlyCollection<string> committedFeatureIds)
    {
        var errors = new List<string>();
        var committedIds = new HashSet<string>(committedFeatureIds, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(output.Persona.PrimaryPersona))
        {
            errors.Add("The primary persona is empty.");
        }

        var featureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in output.Features)
        {
            if (string.IsNullOrWhiteSpace(feature.Id) || !featureIds.Add(feature.Id))
            {
                errors.Add($"Feature id '{feature.Id}' is empty or used more than once.");
            }

            if (string.IsNullOrWhiteSpace(feature.Name) || feature.Name.Length > FeatureConstraints.NameMaxLength)
            {
                errors.Add($"Feature '{feature.Id}' has an empty name or one longer than {FeatureConstraints.NameMaxLength} characters.");
            }

            var isCommitted = committedIds.Contains(feature.Id);
            if (isCommitted != (feature.Origin == FeatureOrigin.FromCommittedList))
            {
                errors.Add($"Feature '{feature.Id}' uses origin from_committed_list incorrectly.");
            }
        }

        foreach (var committedId in committedIds.Where(id => !featureIds.Contains(id)))
        {
            errors.Add($"Committed feature '{committedId}' is missing from the output.");
        }

        foreach (var feature in output.Features)
        {
            foreach (var dependency in feature.DependsOn)
            {
                if (!featureIds.Contains(dependency.FeatureId))
                {
                    errors.Add($"Feature '{feature.Id}' depends on unknown feature '{dependency.FeatureId}'.");
                }
                else if (string.Equals(dependency.FeatureId, feature.Id, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Feature '{feature.Id}' depends on itself.");
                }
            }

            var isCommitted = committedIds.Contains(feature.Id);
            if (feature.PossibleDuplicate && !isCommitted
                && (feature.DuplicateOf is null || !committedIds.Contains(feature.DuplicateOf)))
            {
                errors.Add($"Feature '{feature.Id}' is flagged as a possible duplicate but duplicate_of is not a committed feature id.");
            }
        }

        // Spec step 2.2: the AI may only *propose* a supporting feature if some primary feature depends on it.
        // Committed features are the developer's input, not AI proposals, so the rule doesn't apply to them.
        var dependedOnByPrimary = output.Features
            .Where(feature => feature.Role == FeatureRole.Primary)
            .SelectMany(feature => feature.DependsOn)
            .Select(dependency => dependency.FeatureId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in output.Features.Where(f =>
                     f.Role == FeatureRole.Supporting
                     && !committedIds.Contains(f.Id)
                     && !dependedOnByPrimary.Contains(f.Id)))
        {
            errors.Add($"Supporting feature '{feature.Id}' is not required by any primary feature.");
        }

        return errors;
    }
}
