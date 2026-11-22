namespace ShipMate.Domain.Policies;

public enum ProductionUrlError
{
    None,
    Empty,
    TooLong,
    NotAbsolute,
    NotHttps,
    NotDefaultPort,
    HasCredentials,
    LocalHost
}

/// <summary>
/// What a production URL may look like before ShipMate calls it. SHIP is about a product people can use, so only a
/// public https address on the default port counts; anything else is refused before a single request is made.
/// </summary>
public static class ProductionUrlPolicy
{
    public const int MaxLength = 2048;

    /// <summary>Checks the URL and gives it back in one canonical form: lower-case scheme and host, no fragment.</summary>
    public static ProductionUrlError TryNormalize(string? input, out Uri? url)
    {
        url = null;
        var text = input?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return ProductionUrlError.Empty;
        }

        if (text.Length > MaxLength)
        {
            return ProductionUrlError.TooLong;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed))
        {
            return ProductionUrlError.NotAbsolute;
        }

        var error = Check(parsed);
        if (error != ProductionUrlError.None)
        {
            return error;
        }

        if (string.IsNullOrEmpty(parsed.Host))
        {
            return ProductionUrlError.NotAbsolute;
        }

        // The fragment never reaches the server, and two URLs that differ only by it are the same page.
        url = new Uri(parsed.GetLeftPart(UriPartial.Query));
        return ProductionUrlError.None;
    }

    /// <summary>The same rules for a URL ShipMate is about to follow, such as where a redirect points.</summary>
    public static ProductionUrlError Check(Uri url)
    {
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps)
        {
            return ProductionUrlError.NotHttps;
        }

        // Other ports are where internal services listen; a public product is served on 443.
        if (url.Port != 443)
        {
            return ProductionUrlError.NotDefaultPort;
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            return ProductionUrlError.HasCredentials;
        }

        var host = url.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return ProductionUrlError.LocalHost;
        }

        return ProductionUrlError.None;
    }
}
