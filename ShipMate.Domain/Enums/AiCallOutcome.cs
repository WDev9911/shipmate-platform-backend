namespace ShipMate.Domain.Enums;

/// <summary>How one call to the AI provider ended.</summary>
public enum AiCallOutcome
{
    // The provider returned JSON. Whether the flow then accepts that JSON is not part of this.
    Succeeded,
    RateLimited,
    ProviderUnavailable,
    InvalidOutput,
    Cancelled
}
