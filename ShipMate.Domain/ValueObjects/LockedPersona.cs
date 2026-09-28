namespace ShipMate.Domain.ValueObjects;

public class LockedPersona
{
    public string PrimaryPersona { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<SupportingRole> SupportingRoles { get; set; } = new();
}

public class SupportingRole
{
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
