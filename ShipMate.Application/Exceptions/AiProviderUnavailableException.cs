namespace ShipMate.Application.Exceptions;

public class AiProviderUnavailableException : AppException
{
    public override string ErrorCode => "AI_UNAVAILABLE";
    public override int StatusCode => 503;

    public AiProviderUnavailableException()
        : base("The AI service is unavailable right now. Please try again in a moment.")
    {
    }
}
