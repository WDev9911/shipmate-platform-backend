namespace ShipMate.Domain.Constants;

/// <summary>The 1-5 complexity scale LOCK scores features on (1 = easiest, 5 = hardest).</summary>
public static class ComplexityScale
{
    public const int Min = 1;
    public const int Max = 5;

    public static bool IsValid(int complexity) => complexity is >= Min and <= Max;
}
