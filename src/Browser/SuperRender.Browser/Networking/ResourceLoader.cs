using System.Net;
using System.Text;

namespace SuperRender.Browser.Networking;

/// <summary>
/// Resource loader supporting HTTP(S), file://, and sr:// (embedded test pages) URIs.
/// Integrates cookie jar and HTTP cache.
/// </summary>
public sealed class ResourceLoader : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler _handler;

    public CookieJar? CookieJar { get; set; }
    public HttpCache? Cache { get; set; }

    public ResourceLoader()
    {
        _handler = new HttpClientHandler
        {
            // We manage cookies ourselves via CookieJar
            UseCookies = false,
        };
        _httpClient = new HttpClient(_handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SuperRenderer/1.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Fetch a top-level HTML page. No CORS check needed for navigation.
    /// </summary>
    public async Task<(string html, Uri finalUri)?> FetchHtmlAsync(Uri uri)
    {
        try
        {
            if (uri.Scheme == "sr")
            {
                var content = LoadEmbeddedResource(uri);
                return content is not null ? (content, uri) : null;
            }

            if (uri.Scheme == "file")
            {
                var html = await File.ReadAllTextAsync(uri.LocalPath).ConfigureAwait(false);
                return (html, uri);
            }

            // Check cache
            if (Cache is not null)
            {
                var cached = Cache.Lookup(uri);
                if (cached is { IsFresh: true })
                    return (HttpCache.DecodeBody(cached.Entry), uri);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            AttachCookies(request, uri);

            // Add conditional headers if we have a stale cached version
            if (Cache is not null)
            {
                var stale = Cache.Lookup(uri);
                if (stale is { HasValidators: true })
                    HttpCache.AddConditionalHeaders(request, stale);
            }

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            ProcessSetCookieHeaders(response, uri);

            if (response.StatusCode == HttpStatusCode.NotModified && Cache is not null)
            {
                Cache.UpdateTimestamp(uri);
                var cachedEntry = Cache.Lookup(uri);
                if (cachedEntry is not null)
                    return (HttpCache.DecodeBody(cachedEntry.Entry), uri);
            }

            response.EnsureSuccessStatusCode();
            var bodyBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var text = Encoding.UTF8.GetString(bodyBytes);

            // Store in cache
            Cache?.Store(uri, response, bodyBytes);

            return (text, response.RequestMessage?.RequestUri ?? uri);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Failed to fetch {uri}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fetch a CSS stylesheet with cross-origin checking.
    /// </summary>
    public async Task<string?> FetchCssAsync(Uri uri, Uri originUri)
    {
        try
        {
            if (uri.Scheme == "sr")
                return LoadEmbeddedResource(uri);

            if (uri.Scheme == "file")
            {
                // file:// resources are always allowed from file:// origins
                return await File.ReadAllTextAsync(uri.LocalPath).ConfigureAwait(false);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            AttachCookies(request, uri);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            ProcessSetCookieHeaders(response, uri);

            if (!SecurityPolicy.AllowCrossOriginResponse(uri, originUri, response))
            {
                Console.WriteLine($"[CORS] Blocked cross-origin CSS: {uri} from origin {originUri}");
                return null;
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Failed to fetch CSS {uri}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Fetch a JavaScript file with cross-origin checking.
    /// </summary>
    public async Task<string?> FetchJsAsync(Uri uri, Uri originUri)
    {
        try
        {
            if (uri.Scheme == "sr")
                return LoadEmbeddedResource(uri);

            if (uri.Scheme == "file")
            {
                // file:// resources are always allowed from file:// origins
                return await File.ReadAllTextAsync(uri.LocalPath).ConfigureAwait(false);
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            AttachCookies(request, uri);
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            ProcessSetCookieHeaders(response, uri);

            if (!SecurityPolicy.AllowCrossOriginResponse(uri, originUri, response))
            {
                Console.WriteLine($"[CORS] Blocked cross-origin script: {uri} from origin {originUri}");
                return null;
            }

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Failed to fetch JS {uri}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Perform a fetch request (used by the Fetch API). Returns status, body, and headers.
    /// </summary>
    public async Task<FetchResponse> FetchGenericAsync(
        Uri uri, string method,
        IReadOnlyList<KeyValuePair<string, string>>? headers,
        string? body)
    {
        // Handle sr:// scheme (embedded test pages)
        if (uri.Scheme == "sr")
        {
            var content = LoadEmbeddedResource(uri);
            return new FetchResponse
            {
                Status = content is not null ? 200 : 404,
                StatusText = content is not null ? "OK" : "Not Found",
                Body = content ?? "",
                Url = uri.ToString(),
                Headers = [],
            };
        }

        // Handle file:// scheme
        if (uri.Scheme == "file")
        {
            try
            {
                var fileContent = await File.ReadAllTextAsync(uri.LocalPath).ConfigureAwait(false);
                return new FetchResponse
                {
                    Status = 200,
                    StatusText = "OK",
                    Body = fileContent,
                    Url = uri.ToString(),
                    Headers = [],
                };
            }
            catch
            {
                return new FetchResponse
                {
                    Status = 404,
                    StatusText = "Not Found",
                    Body = "",
                    Url = uri.ToString(),
                    Headers = [],
                };
            }
        }

        var request = new HttpRequestMessage(new HttpMethod(method), uri);
        AttachCookies(request, uri);

        if (headers is not null)
        {
            foreach (var h in headers)
                request.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        ProcessSetCookieHeaders(response, uri);

        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var responseHeaders = new List<KeyValuePair<string, string>>();
        foreach (var h in response.Headers)
            responseHeaders.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));
        foreach (var h in response.Content.Headers)
            responseHeaders.Add(new KeyValuePair<string, string>(h.Key, string.Join(", ", h.Value)));

        return new FetchResponse
        {
            Status = (int)response.StatusCode,
            StatusText = response.ReasonPhrase ?? "",
            Body = responseBody,
            Headers = responseHeaders,
            Url = uri.ToString(),
        };
    }

    private void AttachCookies(HttpRequestMessage request, Uri uri)
    {
        if (CookieJar is null || request.RequestUri is null) return;
        var cookieHeader = CookieJar.GetCookiesForRequest(uri);
        if (!string.IsNullOrEmpty(cookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
    }

    private void ProcessSetCookieHeaders(HttpResponseMessage response, Uri uri)
    {
        if (CookieJar is null) return;
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
        {
            foreach (var setCookie in setCookieValues)
                CookieJar.SetCookie(uri, setCookie);
        }
    }

    private static string? LoadEmbeddedResource(Uri uri)
    {
        var filename = TestPages.GetFilenameFromUri(uri);
        if (filename is null) return null;
        return TestPages.Load(filename);
    }

    /// <summary>
    /// Fetch image binary data. Supports HTTP(S), file://, sr://, and data: URIs.
    /// Returns raw bytes or null on failure.
    /// </summary>
    public async Task<byte[]?> FetchImageBytesAsync(Uri uri, Uri? originUri = null)
    {
        try
        {
            // data: URI (e.g., data:image/png;base64,...)
            if (uri.Scheme == "data")
            {
                return DecodeDataUri(uri.OriginalString);
            }

            if (uri.Scheme == "file")
            {
                return await File.ReadAllBytesAsync(uri.LocalPath).ConfigureAwait(false);
            }

            if (uri.Scheme is "http" or "https")
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.ParseAdd("image/*");

                // Attach cookies
                if (CookieJar is not null)
                {
                    var cookieStr = CookieJar.GetCookiesForRequest(uri);
                    if (!string.IsNullOrEmpty(cookieStr))
                        request.Headers.TryAddWithoutValidation("Cookie", cookieStr);
                }

                using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;

                if (originUri is not null && !SecurityPolicy.AllowCrossOriginResponse(uri, originUri, response))
                    return null;

                // Process response cookies
                if (CookieJar is not null && response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    foreach (var sc in setCookies)
                        CookieJar.SetCookie(uri, sc);
                }

                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ResourceLoader] Image fetch failed for {uri}: {ex.Message}");
        }

        return null;
    }

    private static byte[]? DecodeDataUri(string dataUri)
    {
        // data:[<mediatype>][;base64],<data>
        const string prefix = "data:";
        if (!dataUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var commaIndex = dataUri.IndexOf(',');
        if (commaIndex < 0) return null;

        var meta = dataUri[prefix.Length..commaIndex];
        var data = dataUri[(commaIndex + 1)..];

        if (meta.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            try { return Convert.FromBase64String(data); }
            catch { return null; }
        }

        // Non-base64 data URI (URL-encoded)
        return Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _handler.Dispose();
    }
}

/// <summary>
/// Generic fetch response result.
/// </summary>
public sealed class FetchResponse
{
    public int Status { get; init; }
    public string StatusText { get; init; } = "";
    public string Body { get; init; } = "";
    public string Url { get; init; } = "";
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];
}
