using ShipMate.Domain.Common;
using ShipMate.Domain.Enums;

namespace ShipMate.Domain.Entities;

/// <summary>
/// One call to the AI provider, for the administrator's usage report. Holds no workspace, user, prompt or answer:
/// the administrator sees numbers, never what a workspace asked.
/// </summary>
public class AiCallLog : BaseEntity
{
    public AiCallPurpose Purpose { get; set; }

    /// <summary>The model version the provider reported, or the configured model when it never answered.</summary>
    public string Model { get; set; } = string.Empty;

    public AiCallOutcome Outcome { get; set; }
    public int DurationMs { get; set; }

    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? ThinkingTokens { get; set; }
}
