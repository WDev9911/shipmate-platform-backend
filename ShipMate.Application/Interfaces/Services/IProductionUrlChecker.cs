namespace ShipMate.Application.Interfaces.Services;

/// <summary>Calls the production URL the way SHIP needs it checked, without ever reaching a private network.</summary>
public interface IProductionUrlChecker
{
    /// <param name="url">Already normalized by <c>ProductionUrlPolicy</c>.</param>
    /// <param name="ownershipToken">What the site must serve at <c>ShipOwnership.FilePath</c> on the same host.</param>
    Task<ProductionUrlCheckResult> CheckAsync(Uri url, string ownershipToken, CancellationToken cancellationToken);
}

/// <summary>What each check found. Errors are <see cref="ShipCheckCodes"/> values; the response body is never kept.</summary>
public record ProductionUrlCheckResult(
    int? PingStatusCode,
    string? PingError,
    bool SslValid,
    string? SslError,
    DateTime? SslExpiresAt,
    bool OwnershipVerified,
    string? OwnershipError)
{
    /// <summary>The first check that failed, in the order a person would fix them; null when all passed.</summary>
    public string? FailureCode => PingError switch
    {
        ShipCheckCodes.HostNotPublic => ShipCheckCodes.UrlNotPublic,
        ShipCheckCodes.TlsFailed => ShipCheckCodes.SslInvalid,
        not null => ShipCheckCodes.PingFailed,
        null when PingStatusCode != 200 => ShipCheckCodes.PingFailed,
        null when !SslValid => ShipCheckCodes.SslInvalid,
        null when !OwnershipVerified => ShipCheckCodes.OwnershipUnverified,
        _ => null
    };
}

/// <summary>The fixed strings the checks report. The frontend reads them, so they don't change.</summary>
public static class ShipCheckCodes
{
    // Which check failed first (ShipAttempt.FailureCode)
    public const string UrlNotPublic = "SHIP_URL_NOT_PUBLIC";
    public const string PingFailed = "SHIP_PING_FAILED";
    public const string SslInvalid = "SHIP_SSL_INVALID";
    public const string OwnershipUnverified = "SHIP_OWNERSHIP_UNVERIFIED";

    // Why the page gave no usable answer (PingError)
    public const string HostNotPublic = "HOST_NOT_PUBLIC";
    public const string HostNotFound = "HOST_NOT_FOUND";
    public const string ConnectionFailed = "CONNECTION_FAILED";
    public const string Timeout = "TIMEOUT";
    public const string TlsFailed = "TLS_FAILED";
    public const string TooManyRedirects = "TOO_MANY_REDIRECTS";
    public const string RedirectNotAllowed = "REDIRECT_NOT_ALLOWED";
    public const string UnexpectedStatus = "UNEXPECTED_STATUS";

    // What is wrong with the certificate (SslError)
    public const string CertificateExpired = "CERTIFICATE_EXPIRED";
    public const string CertificateNameMismatch = "NAME_MISMATCH";
    public const string CertificateUntrusted = "UNTRUSTED";
    public const string CertificateMissing = "NOT_PROVIDED";
    public const string CertificateInvalid = "INVALID";
    public const string NotChecked = "NOT_CHECKED";

    // Why ownership isn't proven (OwnershipError)
    public const string FileNotFound = "FILE_NOT_FOUND";
    public const string TokenMismatch = "TOKEN_MISMATCH";
    public const string RedirectedToOtherHost = "REDIRECTED_TO_OTHER_HOST";
    public const string Unreachable = "UNREACHABLE";
}
