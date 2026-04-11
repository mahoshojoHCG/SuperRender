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

        // Manual resolution for custom schemes (e.g. sr://) where Uri.TryCreate fails
        if (baseUri is not null && !string.IsNullOrEmpty(href) && !href.StartsWith('/'))
        {
            // Resolve relative href against the base URI's directory
            var basePath = baseUri.AbsolutePath;
            var lastSlash = basePath.LastIndexOf('/');
            var dir = lastSlash >= 0 ? basePath[..(lastSlash + 1)] : "/";
            var resolvedPath = dir + href;
            if (Uri.TryCreate($"{baseUri.Scheme}://{baseUri.Host}{resolvedPath}", UriKind.Absolute, out var custom))
                return custom;
        }

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

        // Already a valid absolute URI with a known scheme
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "about"
             || uri.Scheme == "file" || uri.Scheme == "sr"))
            return uri;

        // Absolute file path (Unix or Windows)
        if (input.StartsWith('/') || (input.Length >= 3 && input[1] == ':' && (input[2] == '/' || input[2] == '\\')))
            return new Uri("file://" + input);

        // Looks like a domain (contains a dot)
        if (input.Contains('.') && !input.Contains(' '))
            return new Uri("https://" + input);

        // Fallback: treat as https
        return new Uri("https://" + input);
    }
}
