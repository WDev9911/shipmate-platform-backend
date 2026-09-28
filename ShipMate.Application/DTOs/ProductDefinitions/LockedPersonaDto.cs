namespace ShipMate.Application.DTOs.ProductDefinitions;

public class LockedPersonaDto
{
    public string PrimaryPersona { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<SupportingRoleDto> SupportingRoles { get; set; } = new();
}
