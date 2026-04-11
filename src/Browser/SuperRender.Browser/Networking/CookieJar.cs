namespace SuperRender.Browser.Networking;

/// <summary>
/// In-memory cookie storage. Stores cookies per domain/path and handles
/// matching, expiry, and SameSite enforcement.
/// </summary>
public sealed class CookieJar
{
    private readonly List<CookieEntry> _cookies = [];
    private readonly object _lock = new();

    /// <summary>
    /// Parse a Set-Cookie header and store the resulting cookie.
    /// </summary>
    public void SetCookie(Uri origin, string setCookieHeader)
    {
        var cookie = CookieParser.Parse(setCookieHeader, origin);
        if (cookie is null)
            return;

        // If Max-Age is 0 or negative, this is a delete request
        if (cookie.MaxAge.HasValue && cookie.MaxAge.Value <= 0)
        {
            RemoveCookie(cookie.Name, cookie.Domain!, cookie.Path!);
            return;
        }

        // Compute effective expiry from Max-Age (overrides Expires per spec)
        DateTimeOffset? effectiveExpiry = cookie.Expires;
        if (cookie.MaxAge.HasValue)
            effectiveExpiry = DateTimeOffset.UtcNow.AddSeconds(cookie.MaxAge.Value);

        lock (_lock)
        {
            // Remove existing cookie with the same name/domain/path
            _cookies.RemoveAll(c =>
                c.Name == cookie.Name &&
                string.Equals(c.Domain, cookie.Domain, StringComparison.OrdinalIgnoreCase) &&
                c.Path == cookie.Path);

            _cookies.Add(new CookieEntry
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain!,
                Path = cookie.Path!,
                Expires = effectiveExpiry,
                Secure = cookie.Secure,
                HttpOnly = cookie.HttpOnly,
                SameSite = cookie.SameSite,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    /// <summary>
    /// Returns matching cookies for a given request URI as a "name=value; name2=value2" string.
    /// </summary>
    public string GetCookiesForRequest(Uri uri, Uri? originUri = null)
    {
        lock (_lock)
        {
            PurgeExpired();

            var matching = new List<CookieEntry>();
            foreach (var c in _cookies)
            {
                if (!DomainMatches(uri.Host, c.Domain))
                    continue;
                if (!PathMatches(uri.AbsolutePath, c.Path))
                    continue;
                if (c.Secure && uri.Scheme != "https")
                    continue;

                // SameSite enforcement
                if (originUri is not null && !IsSameOriginDomain(originUri.Host, c.Domain))
                {
                    if (c.SameSite == SameSitePolicy.Strict)
                        continue;
                    // Lax only blocks cross-site sub-resource requests; for simplicity,
                    // we allow Lax for navigation-like requests (GET) — but here we always allow
                    if (c.SameSite == SameSitePolicy.None && !c.Secure)
                        continue; // SameSite=None requires Secure
                }

                matching.Add(c);
            }

            // Sort: longer path first, then earlier created first
            matching.Sort((a, b) =>
            {
                int pathCmp = b.Path.Length.CompareTo(a.Path.Length);
                return pathCmp != 0 ? pathCmp : a.CreatedAt.CompareTo(b.CreatedAt);
            });

            return string.Join("; ", matching.Select(c => $"{c.Name}={c.Value}"));
        }
    }

    /// <summary>
    /// Returns all non-HttpOnly cookies visible to JavaScript for the given origin.
    /// Returns in "name=value; name2=value2" format.
    /// </summary>
    public string GetCookiesForScript(Uri origin)
    {
        lock (_lock)
        {
            PurgeExpired();

            var matching = new List<CookieEntry>();
            foreach (var c in _cookies)
            {
                if (c.HttpOnly)
                    continue;
                if (!DomainMatches(origin.Host, c.Domain))
                    continue;
                if (!PathMatches(origin.AbsolutePath, c.Path))
                    continue;
                if (c.Secure && origin.Scheme != "https")
                    continue;

                matching.Add(c);
            }

            return string.Join("; ", matching.Select(c => $"{c.Name}={c.Value}"));
        }
    }

    /// <summary>
    /// Sets a cookie from a JavaScript document.cookie setter (simple name=value with optional attributes).
    /// HttpOnly cookies cannot be set from script.
    /// </summary>
    public void SetCookieFromScript(Uri origin, string cookieString)
    {
        var cookie = CookieParser.Parse(cookieString, origin);
        if (cookie is null)
            return;

        // Scripts cannot set HttpOnly cookies
        if (cookie.HttpOnly)
            return;

        SetCookie(origin, cookieString);
    }

    private void RemoveCookie(string name, string domain, string path)
    {
        lock (_lock)
        {
            _cookies.RemoveAll(c =>
                c.Name == name &&
                string.Equals(c.Domain, domain, StringComparison.OrdinalIgnoreCase) &&
                c.Path == path);
        }
    }

    private void PurgeExpired()
    {
        var now = DateTimeOffset.UtcNow;
        _cookies.RemoveAll(c => c.Expires.HasValue && c.Expires.Value <= now);
    }

    /// <summary>
    /// Domain matching: the request host must equal the cookie domain or be a subdomain.
    /// </summary>
    public static bool DomainMatches(string requestHost, string cookieDomain)
    {
        if (string.Equals(requestHost, cookieDomain, StringComparison.OrdinalIgnoreCase))
            return true;

        // Subdomain match: request host ends with .cookieDomain
        if (requestHost.EndsWith("." + cookieDomain, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Path matching: the request path must start with the cookie path.
    /// </summary>
    public static bool PathMatches(string requestPath, string cookiePath)
    {
        if (requestPath.StartsWith(cookiePath, StringComparison.Ordinal))
        {
            if (cookiePath.EndsWith('/'))
                return true;
            if (requestPath.Length == cookiePath.Length)
                return true;
            if (requestPath.Length > cookiePath.Length && requestPath[cookiePath.Length] == '/')
                return true;
        }
        return false;
    }

    private static bool IsSameOriginDomain(string hostA, string hostB)
    {
        return string.Equals(hostA, hostB, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CookieEntry
    {
        public required string Name { get; init; }
        public required string Value { get; set; }
        public required string Domain { get; init; }
        public required string Path { get; init; }
        public DateTimeOffset? Expires { get; init; }
        public bool Secure { get; init; }
        public bool HttpOnly { get; init; }
        public SameSitePolicy SameSite { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
