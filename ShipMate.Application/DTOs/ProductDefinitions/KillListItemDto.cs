using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class KillListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FeatureRole? Role { get; set; }
    public FeatureOrigin Origin { get; set; }
    public FeatureStatus Status { get; set; }

    // Why the AI judged it unnecessary, when it did.
    public string? AiReason { get; set; }
}
