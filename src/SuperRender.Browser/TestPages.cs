using System.Reflection;

namespace SuperRender.Browser;

/// <summary>
/// Provides access to embedded manual test page resources.
/// Pages are loaded via the <c>sr://test/{name}</c> protocol.
/// </summary>
public static class TestPages
{
    private static readonly Assembly Assembly = typeof(TestPages).Assembly;
    private const string ResourcePrefix = "SuperRender.Browser.TestPages.";

    /// <summary>
    /// Lists all available test page names (without extension for HTML, with extension for CSS/JS).
    /// </summary>
    public static IReadOnlyList<string> ListPages()
    {
        var names = Assembly.GetManifestResourceNames();
        var result = new List<string>();
        foreach (var name in names)
        {
            if (name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                var relative = name[ResourcePrefix.Length..];
                result.Add(relative);
            }
        }
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Loads a test resource by filename (e.g. "index.html", "18-style.css", "18-script.js").
    /// Returns null if the resource does not exist.
    /// </summary>
    public static string? Load(string filename)
    {
        var resourceName = ResourcePrefix + filename;
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Checks whether a test resource exists.
    /// </summary>
    public static bool Exists(string filename)
    {
        var resourceName = ResourcePrefix + filename;
        var info = Assembly.GetManifestResourceInfo(resourceName);
        return info is not null;
    }

    /// <summary>
    /// Extracts the resource filename from an <c>sr://test/</c> URI.
    /// Returns null if the URI is not an <c>sr://test/</c> URI.
    /// </summary>
    public static string? GetFilenameFromUri(Uri uri)
    {
        if (!uri.Scheme.Equals("sr", StringComparison.OrdinalIgnoreCase))
            return null;

        // sr://test/filename.html → Host="test", AbsolutePath="/filename.html"
        if (!uri.Host.Equals("test", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = uri.AbsolutePath.TrimStart('/');
        return string.IsNullOrEmpty(path) ? "index.html" : path;
    }
}
