namespace ShipMate.Application.Exceptions;

/// <summary>
/// Business exception carrying a standardized error code so the FE can branch on it,
/// instead of relying only on the HTTP status code or the message text.
/// </summary>
public abstract class AppException : Exception
{
    public abstract string ErrorCode { get; }
    public abstract int StatusCode { get; }

    // Optional structured data the FE needs to act on the error, returned as "details".
    public virtual object? Details => null;

    protected AppException(string message) : base(message)
    {
    }
}
