namespace ShipMate.Application.DTOs.ProductDefinitions;

public class AddFeatureDependencyRequest
{
    public Guid DependsOnFeatureId { get; set; }
    public string? Reason { get; set; }
}
