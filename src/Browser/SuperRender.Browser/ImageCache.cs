using System.Collections.Concurrent;
using SuperRender.Renderer.Image;

namespace SuperRender.Browser;

/// <summary>
/// Thread-safe in-memory cache for decoded images, keyed by URL.
/// </summary>
public sealed class ImageCache
{
    private readonly ConcurrentDictionary<string, ImageData?> _cache = new(StringComparer.Ordinal);

    /// <summary>Gets a cached image by URL. Returns null if not cached or if the decode failed.</summary>
    public ImageData? Get(string url) => _cache.GetValueOrDefault(url);

    /// <summary>Stores a decoded image (or null for failed decode) in the cache.</summary>
    public void Set(string url, ImageData? data) => _cache[url] = data;

    /// <summary>Returns true if the URL has been cached (including failed decodes).</summary>
    public bool Contains(string url) => _cache.ContainsKey(url);

    /// <summary>Number of cached entries.</summary>
    public int Count => _cache.Count;
}
