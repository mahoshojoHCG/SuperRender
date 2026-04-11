using System.Globalization;

namespace SuperRender.Browser.Networking;

/// <summary>
/// A single parsed cookie.
/// </summary>
public sealed class Cookie
{
    public required string Name { get; init; }
    public required string Value { get; set; }
    public string? Domain { get; set; }
    public string? Path { get; set; }
    public DateTimeOffset? Expires { get; set; }
    public int? MaxAge { get; set; }
    public bool Secure { get; set; }
    public bool HttpOnly { get; set; }
    public SameSitePolicy SameSite { get; set; } = SameSitePolicy.Lax;
}

/// <summary>
/// SameSite cookie policy.
/// </summary>
public enum SameSitePolicy
{
    None,
    Lax,
    Strict,
}

/// <summary>
/// Parses Set-Cookie headers into Cookie objects.
/// </summary>
public static class CookieParser
{
    private static readonly string[] DateFormats =
    [
        "ddd, dd MMM yyyy HH:mm:ss 'GMT'",
        "ddd, dd-MMM-yyyy HH:mm:ss 'GMT'",
        "ddd MMM d HH:mm:ss yyyy",
        "ddd, d MMM yyyy HH:mm:ss 'GMT'",
        "ddd, dd-MMM-yy HH:mm:ss 'GMT'",
    ];

    /// <summary>
    /// Parse a Set-Cookie header value into a Cookie object.
    /// Returns null if the header cannot be parsed.
    /// </summary>
    public static Cookie? Parse(string setCookieHeader, Uri origin)
    {
        if (string.IsNullOrWhiteSpace(setCookieHeader))
            return null;

        var parts = setCookieHeader.Split(';');
        var nameValue = parts[0].Trim();

        var eqIdx = nameValue.IndexOf('=');
        if (eqIdx < 0)
            return null;

        var name = nameValue[..eqIdx].Trim();
        var value = nameValue[(eqIdx + 1)..].Trim();

        if (string.IsNullOrEmpty(name))
            return null;

        var cookie = new Cookie
        {
            Name = name,
            Value = value,
            Domain = origin.Host,
            Path = "/",
        };

        for (int i = 1; i < parts.Length; i++)
        {
            var attr = parts[i].Trim();
            if (string.IsNullOrEmpty(attr))
                continue;

            var attrEq = attr.IndexOf('=');
            string attrName;
            string? attrValue;
            if (attrEq >= 0)
            {
                attrName = attr[..attrEq].Trim();
                attrValue = attr[(attrEq + 1)..].Trim();
            }
            else
            {
                attrName = attr;
                attrValue = null;
            }

            if (attrName.Equals("Domain", StringComparison.OrdinalIgnoreCase) && attrValue is not null)
            {
                // Strip leading dot
                cookie.Domain = attrValue.TrimStart('.');
            }
            else if (attrName.Equals("Path", StringComparison.OrdinalIgnoreCase) && attrValue is not null)
            {
                cookie.Path = attrValue;
            }
            else if (attrName.Equals("Expires", StringComparison.OrdinalIgnoreCase) && attrValue is not null)
            {
                if (DateTimeOffset.TryParseExact(attrValue, DateFormats,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var expires))
                {
                    cookie.Expires = expires;
                }
            }
            else if (attrName.Equals("Max-Age", StringComparison.OrdinalIgnoreCase) && attrValue is not null)
            {
                if (int.TryParse(attrValue, CultureInfo.InvariantCulture, out var maxAge))
                    cookie.MaxAge = maxAge;
            }
            else if (attrName.Equals("Secure", StringComparison.OrdinalIgnoreCase))
            {
                cookie.Secure = true;
            }
            else if (attrName.Equals("HttpOnly", StringComparison.OrdinalIgnoreCase))
            {
                cookie.HttpOnly = true;
            }
            else if (attrName.Equals("SameSite", StringComparison.OrdinalIgnoreCase) && attrValue is not null)
            {
                cookie.SameSite = attrValue.ToLowerInvariant() switch
                {
                    "strict" => SameSitePolicy.Strict,
                    "none" => SameSitePolicy.None,
                    _ => SameSitePolicy.Lax,
                };
            }
        }

        return cookie;
    }
}
