using SuperRender.Browser.Networking;
using Xunit;

namespace SuperRender.Browser.Tests;

public class ResourceLoaderTests : IDisposable
{
    private readonly ResourceLoader _loader = new();
    private readonly string _tempDir;

    public ResourceLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SuperRenderer_Tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        _loader.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    // ═══════════════════════════ FetchHtmlAsync — file:// ═══════════════════════════

    [Fact]
    public async Task FetchHtmlAsync_FileUri_ReadsLocalFile()
    {
        var path = Path.Combine(_tempDir, "test.html");
        await File.WriteAllTextAsync(path, "<html><body>Hello</body></html>");

        var uri = new Uri("file://" + path);
        var result = await _loader.FetchHtmlAsync(uri);

        Assert.NotNull(result);
        Assert.Contains("Hello", result!.Value.html);
        Assert.Equal(uri, result.Value.finalUri);
    }

    [Fact]
    public async Task FetchHtmlAsync_FileUri_NonexistentFile_ReturnsNull()
    {
        var uri = new Uri("file://" + Path.Combine(_tempDir, "missing.html"));
        var result = await _loader.FetchHtmlAsync(uri);

        Assert.Null(result);
    }

    // ═══════════════════════════ FetchCssAsync — file:// ═══════════════════════════

    [Fact]
    public async Task FetchCssAsync_FileUri_ReadsLocalFile()
    {
        var path = Path.Combine(_tempDir, "style.css");
        await File.WriteAllTextAsync(path, "body { color: red; }");

        var uri = new Uri("file://" + path);
        var originUri = new Uri("file://" + Path.Combine(_tempDir, "index.html"));
        var result = await _loader.FetchCssAsync(uri, originUri);

        Assert.NotNull(result);
        Assert.Contains("color: red", result!);
    }

    [Fact]
    public async Task FetchCssAsync_FileUri_NonexistentFile_ReturnsNull()
    {
        var uri = new Uri("file://" + Path.Combine(_tempDir, "missing.css"));
        var originUri = new Uri("file://" + Path.Combine(_tempDir, "index.html"));
        var result = await _loader.FetchCssAsync(uri, originUri);

        Assert.Null(result);
    }

    // ═══════════════════════════ FetchJsAsync — file:// ═══════════════════════════

    [Fact]
    public async Task FetchJsAsync_FileUri_ReadsLocalFile()
    {
        var path = Path.Combine(_tempDir, "app.js");
        await File.WriteAllTextAsync(path, "var x = 42;");

        var uri = new Uri("file://" + path);
        var originUri = new Uri("file://" + Path.Combine(_tempDir, "index.html"));
        var result = await _loader.FetchJsAsync(uri, originUri);

        Assert.NotNull(result);
        Assert.Contains("var x = 42", result!);
    }

    [Fact]
    public async Task FetchJsAsync_FileUri_NonexistentFile_ReturnsNull()
    {
        var uri = new Uri("file://" + Path.Combine(_tempDir, "missing.js"));
        var originUri = new Uri("file://" + Path.Combine(_tempDir, "index.html"));
        var result = await _loader.FetchJsAsync(uri, originUri);

        Assert.Null(result);
    }

    // ═══════════════════════════ Full integration — file:// HTML with resources ═══════════════════════════

    [Fact]
    public async Task FetchHtmlAsync_FileUri_FullDocument_ReadsCorrectly()
    {
        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Test</title></head>
            <body>
                <h1>Test Page</h1>
                <p>Content here.</p>
            </body>
            </html>
            """;
        var path = Path.Combine(_tempDir, "full.html");
        await File.WriteAllTextAsync(path, html);

        var uri = new Uri("file://" + path);
        var result = await _loader.FetchHtmlAsync(uri);

        Assert.NotNull(result);
        Assert.Contains("<h1>Test Page</h1>", result!.Value.html);
        Assert.Contains("<p>Content here.</p>", result.Value.html);
    }

    [Fact]
    public void Resolve_RelativeCssFromFileHtml_ProducesFilePath()
    {
        var baseUri = new Uri("file://" + Path.Combine(_tempDir, "index.html"));
        var resolved = UrlResolver.Resolve("styles/main.css", baseUri);

        Assert.Equal("file", resolved.Scheme);
        Assert.EndsWith("styles/main.css", resolved.LocalPath);
    }
}
