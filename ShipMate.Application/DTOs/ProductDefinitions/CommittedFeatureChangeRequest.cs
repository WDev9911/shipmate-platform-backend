using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>DEFINE step 8: the only way to edit or exclude a feature agreed with the customer.</summary>
public class CommittedFeatureChangeRequest
{
    // Edit or Exclude.
    public FeatureChangeAction Action { get; set; }

    // Edit only — true partial update, a field left null is unchanged.
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Scope { get; set; }

    public string Reason { get; set; } = string.Empty;

    // "I confirm I have informed / will inform the customer about this change."
    public bool CustomerNotifiedConfirmed { get; set; }

    // Exclude only — needed when other included features depend on this one.
    public DependentsResolution? DependentsResolution { get; set; }
}
