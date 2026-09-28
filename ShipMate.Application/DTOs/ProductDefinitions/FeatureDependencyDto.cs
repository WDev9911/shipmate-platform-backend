namespace ShipMate.Application.DTOs.ProductDefinitions;

public class FeatureDependencyDto
{
    public Guid DependsOnFeatureId { get; set; }
    public string? Reason { get; set; }
}
