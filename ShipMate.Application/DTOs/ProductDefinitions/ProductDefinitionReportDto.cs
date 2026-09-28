using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>DEFINE output (b): the report view, built from the same data as the functional list.</summary>
public class ProductDefinitionReportDto
{
    public ProductDefinitionStatus Status { get; set; }

    // Primary persona with its reason, and the supporting roles with theirs.
    public LockedPersonaDto? Persona { get; set; }

    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public List<KillListItemDto> KillList { get; set; } = new();
    public CoreFeatureSummaryDto CoreFeatures { get; set; } = new();

    // Everything still blocking READY_FOR_LOCK: pending features, open flags, dependency cycle.
    public ProductDefinitionReadinessDto Warnings { get; set; } = new();
}
