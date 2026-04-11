using System.Globalization;
using System.Text;

namespace SuperRender.Browser.Networking;

/// <summary>
/// HTTP caching layer. Checks cache before fetch, stores responses after fetch,
/// handles conditional requests (If-None-Match, If-Modified-Since), and parses Cache-Control.
/// </summary>
public sealed class HttpCache : IDisposable
{
    private readonly CacheDatabase _db;

    public HttpCache(string dbPath)
    {
        _db = new CacheDatabase(dbPath);
    }

    public HttpCache(CacheDatabase db)
    {
        _db = db;
    }

    /// <summary>
    /// Look up a cached response. Returns null if not found or expired.
    /// If stale but has validators (ETag/Last-Modified), returns the entry for conditional request.
    /// </summary>
    public CachedResponse? Lookup(Uri uri)
    {
        var entry = _db.Get(uri.ToString());
        if (entry is null)
            return null;

        if (entry.NoStore)
            return null;

        bool fresh = IsFresh(entry);
        bool hasValidators = entry.ETag is not null || entry.LastModified is not null;

        return new CachedResponse
        {
            Entry = entry,
            IsFresh = fresh,
            HasValidators = hasValidators,
        };
    }

    /// <summary>
    /// Stores a response in the cache after a network fetch.
    /// Returns false if Cache-Control: no-store is set.
    /// </summary>
    public bool Store(Uri uri, HttpResponseMessage response, byte[] body)
    {
        var cacheControl = ParseCacheControl(response);

        if (cacheControl.NoStore)
            return false;

        string? etag = null;
        if (response.Headers.ETag is not null)
            etag = response.Headers.ETag.Tag;

        string? lastModified = null;
        if (response.Content.Headers.LastModified.HasValue)
            lastModified = response.Content.Headers.LastModified.Value.ToString("R", CultureInfo.InvariantCulture);

        long? expires = null;
        if (response.Content.Headers.Expires.HasValue)
            expires = response.Content.Headers.Expires.Value.ToUnixTimeSeconds();

        var contentType = response.Content.Headers.ContentType?.MediaType;

        var entry = new CacheEntry
        {
            Url = uri.ToString(),
            Body = body,
            ContentType = contentType,
            ETag = etag,
            LastModified = lastModified,
            Expires = expires,
            MaxAge = cacheControl.MaxAge,
            CachedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            NoStore = false,
        };

        _db.Put(entry);
        return true;
    }

    /// <summary>
    /// Update a cached entry's body and timestamp when receiving a 304 Not Modified.
    /// </summary>
    public void UpdateTimestamp(Uri uri)
    {
        var entry = _db.Get(uri.ToString());
        if (entry is null) return;

        var updated = new CacheEntry
        {
            Url = entry.Url,
            Body = entry.Body,
            ContentType = entry.ContentType,
            ETag = entry.ETag,
            LastModified = entry.LastModified,
            Expires = entry.Expires,
            MaxAge = entry.MaxAge,
            CachedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            NoStore = entry.NoStore,
        };
        _db.Put(updated);
    }

    /// <summary>
    /// Add conditional request headers based on cached validators.
    /// </summary>
    public static void AddConditionalHeaders(HttpRequestMessage request, CachedResponse cached)
    {
        if (cached.Entry.ETag is not null)
            request.Headers.TryAddWithoutValidation("If-None-Match", cached.Entry.ETag);

        if (cached.Entry.LastModified is not null)
            request.Headers.TryAddWithoutValidation("If-Modified-Since", cached.Entry.LastModified);
    }

    /// <summary>
    /// Parse Cache-Control header directives from a response.
    /// </summary>
    public static CacheControlDirectives ParseCacheControl(HttpResponseMessage response)
    {
        var result = new CacheControlDirectives();

        if (!response.Headers.TryGetValues("Cache-Control", out var values))
            return result;

        foreach (var headerVal in values)
        {
            var directives = headerVal.Split(',');
            foreach (var directive in directives)
            {
                var trimmed = directive.Trim().ToLowerInvariant();
                if (trimmed == "no-cache")
                    result.NoCache = true;
                else if (trimmed == "no-store")
                    result.NoStore = true;
                else if (trimmed == "must-revalidate")
                    result.MustRevalidate = true;
                else if (trimmed.StartsWith("max-age=", StringComparison.Ordinal))
                {
                    if (int.TryParse(trimmed.AsSpan(8), CultureInfo.InvariantCulture, out var maxAge))
                        result.MaxAge = maxAge;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Decode the body from a cache entry as a UTF-8 string.
    /// </summary>
    public static string DecodeBody(CacheEntry entry)
    {
        return Encoding.UTF8.GetString(entry.Body);
    }

    private static bool IsFresh(CacheEntry entry)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // max-age takes priority over expires
        if (entry.MaxAge.HasValue)
        {
            long expiresAt = entry.CachedAt + entry.MaxAge.Value;
            return now < expiresAt;
        }

        if (entry.Expires.HasValue)
            return now < entry.Expires.Value;

        // No cache lifetime specified — consider stale
        return false;
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

/// <summary>
/// Parsed Cache-Control directives.
/// </summary>
public sealed class CacheControlDirectives
{
    public bool NoCache { get; set; }
    public bool NoStore { get; set; }
    public bool MustRevalidate { get; set; }
    public int? MaxAge { get; set; }
}

/// <summary>
/// Result of a cache lookup.
/// </summary>
public sealed class CachedResponse
{
    public required CacheEntry Entry { get; init; }
    public bool IsFresh { get; init; }
    public bool HasValidators { get; init; }
}
