using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class FeatureChangeLogDto
{
    public Guid Id { get; set; }
    public FeatureChangeAction Action { get; set; }

    // Always a list: a merge records both features as they were before it.
    public IReadOnlyList<FeatureContentSnapshot> Before { get; set; } = [];
    public FeatureContentSnapshot? After { get; set; }

    public string? Reason { get; set; }
    public bool CustomerNotifiedConfirmed { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
