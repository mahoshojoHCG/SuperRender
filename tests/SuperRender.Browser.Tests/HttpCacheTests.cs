using System.Net;
using System.Text;
using SuperRender.Browser.Networking;
using Xunit;

namespace SuperRender.Browser.Tests;

public class HttpCacheTests : IDisposable
{
    private readonly string _dbPath;
    private readonly CacheDatabase _cacheDb;
    private readonly HttpCache _cache;

    public HttpCacheTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sr_cache_test_{Guid.NewGuid():N}.db");
        _cacheDb = new CacheDatabase(_dbPath);
        _cache = new HttpCache(_cacheDb);
    }

    public void Dispose()
    {
        _cache.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Lookup_NoEntry_ReturnsNull()
    {
        var result = _cache.Lookup(new Uri("https://example.com/page"));
        Assert.Null(result);
    }

    [Fact]
    public void Store_ThenLookup_ReturnsCachedEntry()
    {
        var uri = new Uri("https://example.com/data");
        var body = Encoding.UTF8.GetBytes("hello world");
        var response = CreateResponse(HttpStatusCode.OK, maxAge: 3600);
        _cache.Store(uri, response, body);

        var cached = _cache.Lookup(uri);
        Assert.NotNull(cached);
        Assert.True(cached.IsFresh);
        Assert.Equal("hello world", HttpCache.DecodeBody(cached.Entry));
    }

    [Fact]
    public void Store_NoStore_ReturnsNullOnLookup()
    {
        var uri = new Uri("https://example.com/nostore");
        var body = Encoding.UTF8.GetBytes("secret");
        var response = CreateResponse(HttpStatusCode.OK, noStore: true);
        var stored = _cache.Store(uri, response, body);

        Assert.False(stored);
        Assert.Null(_cache.Lookup(uri));
    }

    [Fact]
    public void Store_ExpiredMaxAge_LookupReturnsStale()
    {
        var uri = new Uri("https://example.com/expired");
        // Manually insert an expired entry
        _cacheDb.Put(new CacheEntry
        {
            Url = uri.ToString(),
            Body = Encoding.UTF8.GetBytes("old data"),
            ContentType = "text/html",
            ETag = "\"etag1\"",
            MaxAge = 0,
            CachedAt = DateTimeOffset.UtcNow.AddSeconds(-100).ToUnixTimeSeconds(),
        });

        var cached = _cache.Lookup(uri);
        Assert.NotNull(cached);
        Assert.False(cached.IsFresh);
        Assert.True(cached.HasValidators);
    }

    [Fact]
    public void Store_WithETag_HasValidators()
    {
        var uri = new Uri("https://example.com/etag");
        var body = Encoding.UTF8.GetBytes("data");
        var response = CreateResponse(HttpStatusCode.OK, maxAge: 3600, etag: "\"v1\"");
        _cache.Store(uri, response, body);

        var cached = _cache.Lookup(uri);
        Assert.NotNull(cached);
        Assert.True(cached.HasValidators);
        Assert.Equal("\"v1\"", cached.Entry.ETag);
    }

    [Fact]
    public void AddConditionalHeaders_AddsIfNoneMatch()
    {
        var cached = new CachedResponse
        {
            Entry = new CacheEntry
            {
                Url = "https://example.com/",
                Body = [],
                ETag = "\"abc\"",
                CachedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
            IsFresh = false,
            HasValidators = true,
        };

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        HttpCache.AddConditionalHeaders(request, cached);

        Assert.Contains("\"abc\"", request.Headers.GetValues("If-None-Match"));
    }

    [Fact]
    public void AddConditionalHeaders_AddsIfModifiedSince()
    {
        var cached = new CachedResponse
        {
            Entry = new CacheEntry
            {
                Url = "https://example.com/",
                Body = [],
                LastModified = "Wed, 01 Jan 2025 00:00:00 GMT",
                CachedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            },
            IsFresh = false,
            HasValidators = true,
        };

        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        HttpCache.AddConditionalHeaders(request, cached);

        Assert.Contains("Wed, 01 Jan 2025 00:00:00 GMT", request.Headers.GetValues("If-Modified-Since"));
    }

    [Fact]
    public void ParseCacheControl_MaxAge()
    {
        var response = CreateResponse(HttpStatusCode.OK, maxAge: 600);
        var cc = HttpCache.ParseCacheControl(response);
        Assert.Equal(600, cc.MaxAge);
        Assert.False(cc.NoStore);
        Assert.False(cc.NoCache);
    }

    [Fact]
    public void ParseCacheControl_NoStore()
    {
        var response = CreateResponse(HttpStatusCode.OK, noStore: true);
        var cc = HttpCache.ParseCacheControl(response);
        Assert.True(cc.NoStore);
    }

    [Fact]
    public void ParseCacheControl_MustRevalidate()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("Cache-Control", "must-revalidate, max-age=0");
        var cc = HttpCache.ParseCacheControl(response);
        Assert.True(cc.MustRevalidate);
        Assert.Equal(0, cc.MaxAge);
    }

    [Fact]
    public void UpdateTimestamp_RefreshesCachedAt()
    {
        var uri = new Uri("https://example.com/refresh");
        var oldTime = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        _cacheDb.Put(new CacheEntry
        {
            Url = uri.ToString(),
            Body = Encoding.UTF8.GetBytes("data"),
            MaxAge = 3600,
            CachedAt = oldTime,
        });

        _cache.UpdateTimestamp(uri);
        var updated = _cacheDb.Get(uri.ToString());
        Assert.NotNull(updated);
        Assert.True(updated.CachedAt > oldTime);
    }

    [Fact]
    public void DecodeBody_UTF8()
    {
        var entry = new CacheEntry
        {
            Url = "https://example.com/",
            Body = Encoding.UTF8.GetBytes("hello"),
            CachedAt = 0,
        };
        Assert.Equal("hello", HttpCache.DecodeBody(entry));
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode status,
        int? maxAge = null,
        bool noStore = false,
        string? etag = null)
    {
        var response = new HttpResponseMessage(status);
        var ccParts = new List<string>();
        if (maxAge.HasValue)
            ccParts.Add($"max-age={maxAge.Value}");
        if (noStore)
            ccParts.Add("no-store");
        if (ccParts.Count > 0)
            response.Headers.TryAddWithoutValidation("Cache-Control", string.Join(", ", ccParts));
        if (etag is not null)
            response.Headers.TryAddWithoutValidation("ETag", etag);
        return response;
    }
}
