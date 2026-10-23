namespace ShipMate.Application.Exceptions;

public class FeatureAlreadyAssignedException : AppException
{
    public override string ErrorCode => "FEATURE_ALREADY_ASSIGNED";
    public override int StatusCode => 409;

    public FeatureAlreadyAssignedException()
        : base("Another developer already took this feature.")
    {
    }
}

public class SkillNotDeclaredException : AppException
{
    public override string ErrorCode => "SKILL_NOT_DECLARED";
    public override int StatusCode => 409;

    public SkillNotDeclaredException()
        : base("Declare your skill level in this workspace before taking a feature.")
    {
    }
}

public class SkillTooLowForFeatureException : AppException
{
    public override string ErrorCode => "SKILL_TOO_LOW_FOR_FEATURE";
    public override int StatusCode => 409;

    public SkillTooLowForFeatureException()
        : base("Your skill level is below this feature's complexity.")
    {
    }
}
