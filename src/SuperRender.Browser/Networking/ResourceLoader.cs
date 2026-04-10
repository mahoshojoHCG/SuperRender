namespace SuperRender.Browser.Networking;

/// <summary>
/// HTTP resource loader with cross-origin checking support.
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
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return (html, response.RequestMessage?.RequestUri ?? uri);
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

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
