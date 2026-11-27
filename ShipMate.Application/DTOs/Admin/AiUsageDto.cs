using ShipMate.Domain.Enums;

namespace ShipMate.Application.DTOs.Admin;

public class AiUsageQuery
{
    /// <summary>UTC. Defaults to 30 days before <see cref="To"/>.</summary>
    public DateTime? From { get; set; }

    /// <summary>UTC. Defaults to now.</summary>
    public DateTime? To { get; set; }
}

public class AiUsageDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    /// <summary>One row per day (UTC) and flow that called the AI at least once.</summary>
    public List<AiUsageRowDto> Rows { get; set; } = [];
}

public class AiUsageRowDto
{
    public DateTime Day { get; set; }
    public AiCallPurpose Purpose { get; set; }
    public int Calls { get; set; }
    public int Succeeded { get; set; }
    public int RateLimited { get; set; }
    public int ProviderUnavailable { get; set; }
    public int InvalidOutput { get; set; }
    public int Cancelled { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long ThinkingTokens { get; set; }
    public double AverageDurationMs { get; set; }
}
