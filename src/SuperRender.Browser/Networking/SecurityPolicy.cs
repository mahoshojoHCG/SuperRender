namespace SuperRender.Browser.Networking;

/// <summary>
/// Cross-origin security policy. Checks same-origin and CORS headers.
/// </summary>
public sealed class SecurityPolicy
{
    /// <summary>
    /// Checks if two URIs share the same origin (scheme + host + port).
    /// </summary>
    public static bool IsSameOrigin(Uri a, Uri b)
    {
        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port;
    }

    /// <summary>
    /// Determines if a cross-origin sub-resource response should be allowed.
    /// Checks the Access-Control-Allow-Origin header.
    /// </summary>
    public static bool AllowCrossOriginResponse(
        Uri requestUri, Uri originUri, HttpResponseMessage response)
    {
        // Same-origin is always allowed
        if (IsSameOrigin(requestUri, originUri))
            return true;

        // Check CORS header
        if (response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values))
        {
            foreach (var value in values)
            {
                if (value == "*")
                    return true;

                var originString = $"{originUri.Scheme}://{originUri.Host}";
                if (originUri.Port != 80 && originUri.Port != 443)
                    originString += $":{originUri.Port}";

                if (string.Equals(value.Trim(), originString, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
