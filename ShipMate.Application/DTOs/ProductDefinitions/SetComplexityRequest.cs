namespace ShipMate.Application.DTOs.ProductDefinitions;

public class SetComplexityRequest
{
    public List<FeatureComplexityItem> Items { get; set; } = new();
}

public class FeatureComplexityItem
{
    public Guid FeatureId { get; set; }
    public int Complexity { get; set; }
}
