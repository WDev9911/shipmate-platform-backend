namespace ShipMate.Application.DTOs.ProductDefinitions;

public class UpdateFeatureRequest
{
    // True partial update: a field left null means "don't change it".
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Scope { get; set; }
}
