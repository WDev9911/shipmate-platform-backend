namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>What to do with included features that depend on a feature being excluded (DEFINE step 5).</summary>
public enum DependentsResolution
{
    RemoveLinks,
    ExcludeDependents
}
