namespace ShipMate.Domain.Policies;

/// <summary>What the score is computed from, already counted. Every count is zero or more.</summary>
public record ScopeDisciplineCounts(
    // Feature Challenges approved (SCOPE_CHANGE), of which how many removed a feature and how many only added one.
    int ScopeChanges,
    int RemoveAndReplace,
    int AddDirectly,
    // Manager overrides of a drift alert (SCOPE_BYPASS).
    int Bypasses,
    // Drift threshold lowered (GOVERNANCE_DILUTION).
    int Dilutions,
    // Drift alerts still open on a feature the shipped version builds.
    int OpenDrift,
    // Whole days after the launch deadline; zero without a deadline.
    int DaysLate,
    // Code that reached a protected branch without going through ShipMate (DIRECT_PUSH_DETECTED).
    int DirectPushes);

/// <summary>One line of the score: what was counted and how much it took off, out of the most it can take.</summary>
public record ScoreComponent(string Key, int Count, int Deduction, int Cap, int? OutOf = null);

public record ScopeDisciplineScore(int Score, string FormulaVersion, IReadOnlyList<ScoreComponent> Components);

/// <summary>
/// The Scope Discipline Score on a Ship Card. It starts at 100 and loses points for every way the team stepped
/// around the locked scope. Each part has a cap, so one kind of slip can't take the whole score, and the card can
/// say exactly where every point went. The weights are a draft: changing them means changing this file and the
/// version, so cards minted before keep saying which formula they used.
/// </summary>
public static class ScopeDisciplinePolicy
{
    public const string FormulaVersion = "v0";
    public const int MaxScore = 100;

    public static class Keys
    {
        public const string ScopeVolatility = "scope_volatility";
        public const string ScopeBypass = "scope_bypass";
        public const string GovernanceDilution = "governance_dilution";
        public const string OpenDrift = "open_drift";
        public const string RemoveAndReplace = "remove_and_replace";
        public const string DeadlineOverrun = "deadline_overrun";
        public const string DirectPush = "direct_push";
    }

    private const int VolatilityPerChange = 3, VolatilityCap = 15;
    private const int BypassPerOverride = 5, BypassCap = 25;
    private const int DilutionPerLowering = 10, DilutionCap = 20;
    private const int DriftPerAlert = 5, DriftCap = 15;
    private const int RemoveAndReplaceCap = 10;
    private const int LatePerDay = 1, LateCap = 15;
    private const int DirectPushPerEvent = 5, DirectPushCap = 10;

    public static ScopeDisciplineScore Compute(ScopeDisciplineCounts counts)
    {
        var components = new List<ScoreComponent>
        {
            PerEvent(Keys.ScopeVolatility, counts.ScopeChanges, VolatilityPerChange, VolatilityCap),
            PerEvent(Keys.ScopeBypass, counts.Bypasses, BypassPerOverride, BypassCap),
            PerEvent(Keys.GovernanceDilution, counts.Dilutions, DilutionPerLowering, DilutionCap),
            PerEvent(Keys.OpenDrift, counts.OpenDrift, DriftPerAlert, DriftCap),
            RemoveAndReplaceShare(counts.RemoveAndReplace, counts.AddDirectly),
            PerEvent(Keys.DeadlineOverrun, counts.DaysLate, LatePerDay, LateCap),
            PerEvent(Keys.DirectPush, counts.DirectPushes, DirectPushPerEvent, DirectPushCap)
        };

        var score = Math.Clamp(MaxScore - components.Sum(c => c.Deduction), 0, MaxScore);
        return new ScopeDisciplineScore(score, FormulaVersion, components);
    }

    private static ScoreComponent PerEvent(string key, int count, int perEvent, int cap)
    {
        var safeCount = Math.Max(count, 0);
        // long, so a huge count can't overflow into a negative deduction.
        return new ScoreComponent(key, safeCount, (int)Math.Min((long)safeCount * perEvent, cap), cap);
    }

    // The share of scope changes that took a feature away rather than only adding one. Backups aren't counted:
    // they add work nobody is held to. No change at all is a share of zero.
    private static ScoreComponent RemoveAndReplaceShare(int removeAndReplace, int addDirectly)
    {
        var removed = Math.Max(removeAndReplace, 0);
        var total = removed + Math.Max(addDirectly, 0);
        var deduction = total == 0
            ? 0
            : (int)Math.Round((decimal)removed / total * RemoveAndReplaceCap, MidpointRounding.AwayFromZero);

        return new ScoreComponent(Keys.RemoveAndReplace, removed, Math.Min(deduction, RemoveAndReplaceCap), RemoveAndReplaceCap, total);
    }

    /// <summary>Whole days between the deadline's day and the ship day; zero when on time or without a deadline.</summary>
    public static int DaysLate(DateTime? launchDeadline, DateTime shippedAt)
    {
        if (launchDeadline is null)
        {
            return 0;
        }

        // The deadline is a day, not an instant: shipping any time on that day is on time.
        var days = (shippedAt.Date - launchDeadline.Value.Date).Days;
        return Math.Max(days, 0);
    }
}
