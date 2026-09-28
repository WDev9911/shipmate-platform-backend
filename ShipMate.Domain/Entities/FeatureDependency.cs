using ShipMate.Domain.Common;

namespace ShipMate.Domain.Entities;

public class FeatureDependency : BaseEntity
{
    public Guid FeatureId { get; set; }
    public Feature Feature { get; set; } = null!;

    public Guid DependsOnFeatureId { get; set; }
    public Feature DependsOnFeature { get; set; } = null!;

    public string? Reason { get; set; }
}
