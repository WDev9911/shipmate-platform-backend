namespace ShipMate.Application.Exceptions;

public class AiRateLimitedException : AppException
{
    public override string ErrorCode => "AI_RATE_LIMITED";
    public override int StatusCode => 429;

    public AiRateLimitedException()
        : base("The AI service's request limit has been reached. Please try again later.")
    {
    }
}
