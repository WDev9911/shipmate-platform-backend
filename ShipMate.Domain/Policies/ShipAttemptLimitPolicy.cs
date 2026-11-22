namespace ShipMate.Domain.Policies;

/// <summary>
/// Each attempt makes ShipMate's server call an address someone typed in. Limiting how often keeps that from being
/// used to hammer a site, while leaving plenty of room to fix a deployment and try again.
/// </summary>
public static class ShipAttemptLimitPolicy
{
    public const int MaxAttemptsPerWindow = 10;
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);

    /// <summary>Null when another attempt may run now; otherwise when the oldest attempt in the window leaves it.</summary>
    public static DateTime? RetryAfter(IReadOnlyCollection<DateTime> attemptTimes, DateTime now)
    {
        var inWindow = attemptTimes
            .Where(t => t > now - Window)
            .OrderBy(t => t)
            .ToList();

        if (inWindow.Count < MaxAttemptsPerWindow)
        {
            return null;
        }

        // The window has to drop enough attempts to leave room for one more.
        return inWindow[inWindow.Count - MaxAttemptsPerWindow] + Window;
    }
}
