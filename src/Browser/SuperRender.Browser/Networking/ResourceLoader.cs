namespace SuperRender.Browser.Networking;

/// <summary>
/// Resource loader supporting HTTP(S), file://, and sr:// (embedded test pages) URIs.
/// </summary>
public sealed class ResourceLoader : IDisposable
{
    private readonly HttpClient _httpClient;

    public ResourceLoader()
    {
        _httpClient = new HttpClient();
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

            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

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

            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

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

    private static string? LoadEmbeddedResource(Uri uri)
    {
        var filename = TestPages.GetFilenameFromUri(uri);
        if (filename is null) return null;
        return TestPages.Load(filename);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
