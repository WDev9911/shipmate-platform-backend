namespace ShipMate.Domain.ValueObjects;

/// <summary>Optional rules the product must respect, as free text. Every part may be empty.</summary>
public class ProjectConstraints
{
    public string? Customer { get; set; }
    public string? Company { get; set; }
    public string? Team { get; set; }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Customer) && string.IsNullOrWhiteSpace(Company) && string.IsNullOrWhiteSpace(Team);
}
