namespace ShipMate.Application.Exceptions;

public class AiOutputInvalidException : AppException
{
    public override string ErrorCode => "AI_OUTPUT_INVALID";
    public override int StatusCode => 502;

    // What the AI actually returned, kept so the caller can record it for debugging.
    public string? RawResponse { get; }

    public AiOutputInvalidException(string? rawResponse)
        : base("The AI returned an incomplete or invalid result. Please try running the analysis again.")
    {
        RawResponse = rawResponse;
    }
}
