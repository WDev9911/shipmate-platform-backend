using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.ProductDefinitions;

public class AiAssessmentDto
{
    public AiVerdict Verdict { get; set; }
    public string? Reason { get; set; }
}
