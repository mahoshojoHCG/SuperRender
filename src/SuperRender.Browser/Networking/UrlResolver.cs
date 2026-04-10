namespace SuperRender.Browser.Networking;

/// <summary>
/// Resolves relative URLs against a base URI.
/// </summary>
public static class UrlResolver
{
    public static Uri Resolve(string href, Uri? baseUri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri))
            return absoluteUri;

        if (baseUri is not null && Uri.TryCreate(baseUri, href, out var resolved))
            return resolved;

        // Last resort: try as-is
        return new Uri(href, UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// Normalize a user-typed address string into a proper URI.
    /// </summary>
    public static Uri NormalizeAddress(string input)
    {
        input = input.Trim();

        if (string.IsNullOrEmpty(input))
            return new Uri("about:blank");

        // Already a valid absolute URI
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "about"))
            return uri;

        // Looks like a domain (contains a dot)
        if (input.Contains('.') && !input.Contains(' '))
            return new Uri("https://" + input);

        // Fallback: treat as https
        return new Uri("https://" + input);
    }
}
