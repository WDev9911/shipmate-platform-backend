namespace ShipMate.Application.DTOs.ProductDefinitions;

// Replaces all three parts; a null or blank part clears it.
public class UpdateProjectConstraintsRequest
{
    public string? Customer { get; set; }
    public string? Company { get; set; }
    public string? Team { get; set; }
}
