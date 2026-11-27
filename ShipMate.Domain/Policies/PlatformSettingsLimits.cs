namespace ShipMate.Domain.Policies;

/// <summary>What an administrator may put into the platform settings.</summary>
public static class PlatformSettingsLimits
{
    public const int MinBudgetPerDeveloper = 5;
    public const int MaxBudgetPerDeveloper = 50;

    public const int MinPatterns = 1;
    public const int MaxPatterns = 30;
    public const int MinPatternLength = 3;
    public const int MaxPatternLength = 50;

    /// <summary>Patterns are compared without case and without the spaces around them, so they are kept that way.</summary>
    public static List<string> NormalizePatterns(IEnumerable<string> patterns) =>
        patterns.Select(p => p.Trim().ToLowerInvariant()).Distinct().ToList();
}
