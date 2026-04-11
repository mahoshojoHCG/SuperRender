using SuperRender.Browser.Networking;
using Xunit;

namespace SuperRender.Browser.Tests;

public class TestPagesTests
{
    // ═══════════════════════════ TestPages.Load ═══════════════════════════

    [Fact]
    public void Load_IndexHtml_ReturnsContent()
    {
        var content = TestPages.Load("index.html");
        Assert.NotNull(content);
        Assert.Contains("SuperRenderer", content!);
    }

    [Fact]
    public void Load_BoxSizingHtml_ReturnsContent()
    {
        var content = TestPages.Load("P0/01-box-sizing.html");
        Assert.NotNull(content);
        Assert.Contains("box-sizing", content!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_ExternalStyleCss_ReturnsContent()
    {
        var content = TestPages.Load("P0/18-style.css");
        Assert.NotNull(content);
        Assert.Contains("color", content!);
    }

    [Fact]
    public void Load_ExternalScriptJs_ReturnsContent()
    {
        var content = TestPages.Load("P0/18-script.js");
        Assert.NotNull(content);
        Assert.Contains("document", content!);
    }

    [Fact]
    public void Load_NonexistentFile_ReturnsNull()
    {
        var content = TestPages.Load("nonexistent.html");
        Assert.Null(content);
    }

    // ═══════════════════════════ TestPages.Exists ═══════════════════════════

    [Fact]
    public void Exists_IndexHtml_ReturnsTrue()
    {
        Assert.True(TestPages.Exists("index.html"));
    }

    [Fact]
    public void Exists_NonexistentFile_ReturnsFalse()
    {
        Assert.False(TestPages.Exists("nonexistent.html"));
    }

    // ═══════════════════════════ TestPages.ListPages ═══════════════════════════

    [Fact]
    public void ListPages_ContainsAllTestFiles()
    {
        var pages = TestPages.ListPages();
        Assert.Contains("index.html", pages);
        Assert.Contains("P0.01-box-sizing.html", pages);
        Assert.Contains("P0.18-style.css", pages);
        Assert.Contains("P0.18-script.js", pages);
        Assert.True(pages.Count >= 20, $"Expected at least 20 resources, got {pages.Count}");
    }

    // ═══════════════════════════ TestPages.GetFilenameFromUri ═══════════════════════════

    [Fact]
    public void GetFilenameFromUri_ValidSrUri_ReturnsFilename()
    {
        var uri = new Uri("sr://test/01-box-sizing.html");
        var filename = TestPages.GetFilenameFromUri(uri);
        Assert.Equal("01-box-sizing.html", filename);
    }

    [Fact]
    public void GetFilenameFromUri_SrTestRoot_ReturnsIndex()
    {
        var uri = new Uri("sr://test/");
        var filename = TestPages.GetFilenameFromUri(uri);
        Assert.Equal("index.html", filename);
    }

    [Fact]
    public void GetFilenameFromUri_HttpUri_ReturnsNull()
    {
        var uri = new Uri("https://example.com/page.html");
        var filename = TestPages.GetFilenameFromUri(uri);
        Assert.Null(filename);
    }

    [Fact]
    public void GetFilenameFromUri_WrongHost_ReturnsNull()
    {
        var uri = new Uri("sr://other/page.html");
        var filename = TestPages.GetFilenameFromUri(uri);
        Assert.Null(filename);
    }

    // ═══════════════════════════ UrlResolver — sr:// ═══════════════════════════

    [Fact]
    public void NormalizeAddress_SrUri_Preserved()
    {
        var result = UrlResolver.NormalizeAddress("sr://test/index.html");
        Assert.Equal("sr", result.Scheme);
        Assert.Equal("test", result.Host);
    }

    [Fact]
    public void Resolve_RelativeToSrBase_ReturnsSrUri()
    {
        var baseUri = new Uri("sr://test/index.html");
        var result = UrlResolver.Resolve("01-box-sizing.html", baseUri);

        Assert.Equal("sr", result.Scheme);
        Assert.Equal("test", result.Host);
        Assert.Contains("01-box-sizing.html", result.AbsolutePath);
    }

    [Fact]
    public void Resolve_CssRelativeToSrBase_ReturnsSrUri()
    {
        var baseUri = new Uri("sr://test/P0/18-external-resources.html");
        var result = UrlResolver.Resolve("18-style.css", baseUri);

        Assert.Equal("sr", result.Scheme);
        Assert.Contains("18-style.css", result.AbsolutePath);
    }

    // ═══════════════════════════ ResourceLoader — sr:// ═══════════════════════════

    [Fact]
    public async Task FetchHtmlAsync_SrUri_ReturnsEmbeddedContent()
    {
        using var loader = new ResourceLoader();
        var uri = new Uri("sr://test/index.html");
        var result = await loader.FetchHtmlAsync(uri);

        Assert.NotNull(result);
        Assert.Contains("SuperRenderer", result!.Value.html);
        Assert.Equal(uri, result.Value.finalUri);
    }

    [Fact]
    public async Task FetchHtmlAsync_SrUri_NonexistentPage_ReturnsNull()
    {
        using var loader = new ResourceLoader();
        var uri = new Uri("sr://test/nonexistent.html");
        var result = await loader.FetchHtmlAsync(uri);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchCssAsync_SrUri_ReturnsEmbeddedCss()
    {
        using var loader = new ResourceLoader();
        var cssUri = new Uri("sr://test/P0/18-style.css");
        var originUri = new Uri("sr://test/P0/18-external-resources.html");
        var result = await loader.FetchCssAsync(cssUri, originUri);

        Assert.NotNull(result);
        Assert.Contains("color", result!);
    }

    [Fact]
    public async Task FetchJsAsync_SrUri_ReturnsEmbeddedJs()
    {
        using var loader = new ResourceLoader();
        var jsUri = new Uri("sr://test/P0/18-script.js");
        var originUri = new Uri("sr://test/P0/18-external-resources.html");
        var result = await loader.FetchJsAsync(jsUri, originUri);

        Assert.NotNull(result);
        Assert.Contains("document", result!);
    }

    // ═══════════════════════════ End-to-end: sr://test/ loads all test pages ═══════════════════════════

    [Theory]
    [InlineData("index.html")]
    [InlineData("P0/01-box-sizing.html")]
    [InlineData("P0/02-overflow.html")]
    [InlineData("P0/03-inline-block.html")]
    [InlineData("P0/04-position.html")]
    [InlineData("P0/05-white-space.html")]
    [InlineData("P0/06-ua-stylesheet.html")]
    [InlineData("P0/07-scrolling.html")]
    [InlineData("P0/08-links.html")]
    [InlineData("P0/09-keyboard-shortcuts.html")]
    [InlineData("P0/10-text-selection.html")]
    [InlineData("P0/11-context-menu.html")]
    [InlineData("P0/12-devtools.html")]
    [InlineData("P0/13-timers.html")]
    [InlineData("P0/14-dom-events.html")]
    [InlineData("P0/15-generators.html")]
    [InlineData("P0/16-async-await.html")]
    [InlineData("P0/17-integration.html")]
    [InlineData("P0/18-external-resources.html")]
    public async Task FetchHtmlAsync_AllTestPages_LoadSuccessfully(string page)
    {
        using var loader = new ResourceLoader();
        var uri = new Uri($"sr://test/{page}");
        var result = await loader.FetchHtmlAsync(uri);

        Assert.NotNull(result);
        Assert.True(result!.Value.html.Length > 100, $"Page {page} content too short: {result.Value.html.Length} chars");
    }
}
