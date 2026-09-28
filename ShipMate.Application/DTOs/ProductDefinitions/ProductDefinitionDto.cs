using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class ProductDefinitionDto
{
    // Null while the workspace has no stored product definition yet (nothing has been written).
    public Guid? Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public ProductDefinitionStatus Status { get; set; }
    public LockedPersonaDto? LockedPersona { get; set; }
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public List<FeatureDto> Features { get; set; } = new();
    public DateTime? UpdatedAt { get; set; }
}
