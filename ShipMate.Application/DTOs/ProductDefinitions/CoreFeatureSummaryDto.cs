namespace ShipMate.Application.DTOs.ProductDefinitions;

/// <summary>Numbers behind the soft "3-5 core features" recommendation; the FE writes the message.</summary>
public class CoreFeatureSummaryDto
{
    public int PrimaryCount { get; set; }
    public int SupportingCount { get; set; }
    public int RecommendedMin { get; set; }
    public int RecommendedMax { get; set; }
    public bool ExceedsRecommendation { get; set; }
}
