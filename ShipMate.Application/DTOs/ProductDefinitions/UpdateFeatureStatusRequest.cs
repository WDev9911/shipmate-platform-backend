using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class UpdateFeatureStatusRequest
{
    public FeatureStatus Status { get; set; }

    // Only used when excluding a feature that other included features depend on.
    public DependentsResolution? DependentsResolution { get; set; }
}
