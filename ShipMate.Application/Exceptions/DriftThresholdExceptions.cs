using ShipMate.Domain.Policies;

namespace ShipMate.Application.Exceptions;

public class ThresholdOutOfRangeException : AppException
{
    public override string ErrorCode => "THRESHOLD_OUT_OF_RANGE";
    public override int StatusCode => 400;
    public override object Details => new { Min = DriftThresholdPolicy.Min, Max = DriftThresholdPolicy.Max };

    public ThresholdOutOfRangeException()
        : base($"The drift threshold must be between {DriftThresholdPolicy.Min} and {DriftThresholdPolicy.Max}.")
    {
    }
}

public class ThresholdUnchangedException : AppException
{
    public override string ErrorCode => "THRESHOLD_UNCHANGED";
    public override int StatusCode => 400;

    public ThresholdUnchangedException()
        : base("The drift threshold already has that value.")
    {
    }
}

public class ThresholdChangeCooldownException : AppException
{
    private readonly DateTime _nextChangeAllowedAt;

    public override string ErrorCode => "THRESHOLD_CHANGE_COOLDOWN";
    public override int StatusCode => 409;
    public override object Details => new { NextChangeAllowedAt = _nextChangeAllowedAt };

    public ThresholdChangeCooldownException(DateTime nextChangeAllowedAt)
        : base("The drift threshold was changed recently. It can be changed once every 7 days.")
    {
        _nextChangeAllowedAt = nextChangeAllowedAt;
    }
}

public class ThresholdChangeLimitReachedException : AppException
{
    public override string ErrorCode => "THRESHOLD_CHANGE_LIMIT_REACHED";
    public override int StatusCode => 409;

    public ThresholdChangeLimitReachedException()
        : base($"The drift threshold can be changed at most {DriftThresholdPolicy.MaxChanges} times in a project, and all of them are used.")
    {
    }
}

/// <summary>Two changes of the same threshold raced; the one that lost must look at the new value first.</summary>
public class ThresholdChangeConflictException : AppException
{
    public override string ErrorCode => "THRESHOLD_CHANGE_CONFLICT";
    public override int StatusCode => 409;

    public ThresholdChangeConflictException()
        : base("The drift threshold was just changed by someone else. Reload and try again.")
    {
    }
}
