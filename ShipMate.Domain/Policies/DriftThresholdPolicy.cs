namespace ShipMate.Domain.Policies;

public enum ThresholdChangeVerdict
{
    Allowed,
    OutOfRange,
    Unchanged,
    LimitReached,
    CoolingDown
}

/// <summary>
/// Who may move the drift threshold, by how much and how often. A lower threshold lets code through that the AI
/// would have flagged, so it is the quiet way around BUILD; the limits make it slow and visible.
/// </summary>
public static class DriftThresholdPolicy
{
    // The system never lets the threshold go below this, whatever a Manager asks for.
    public const decimal HardFloor = 0.65m;

    public const decimal Min = 0.70m;
    public const decimal Max = 0.85m;

    // Over the whole life of the project, and no sooner than this after the last change.
    public const int MaxChanges = 3;
    public static readonly TimeSpan Cooldown = TimeSpan.FromDays(7);

    /// <param name="previousChangeTimes">When each earlier change was made. Raising counts as a change just as lowering does.</param>
    public static ThresholdChangeVerdict Check(
        decimal current, decimal requested, IReadOnlyCollection<DateTime> previousChangeTimes, DateTime now)
    {
        if (requested < Min || requested > Max || requested < HardFloor)
        {
            return ThresholdChangeVerdict.OutOfRange;
        }

        if (requested == current)
        {
            return ThresholdChangeVerdict.Unchanged;
        }

        if (previousChangeTimes.Count >= MaxChanges)
        {
            return ThresholdChangeVerdict.LimitReached;
        }

        if (now < NextAllowedChange(previousChangeTimes))
        {
            return ThresholdChangeVerdict.CoolingDown;
        }

        return ThresholdChangeVerdict.Allowed;
    }

    /// <summary>When the next change may be made; the earliest possible time while no change has been made yet.</summary>
    public static DateTime NextAllowedChange(IReadOnlyCollection<DateTime> previousChangeTimes) =>
        previousChangeTimes.Count == 0 ? DateTime.MinValue : previousChangeTimes.Max() + Cooldown;
}
