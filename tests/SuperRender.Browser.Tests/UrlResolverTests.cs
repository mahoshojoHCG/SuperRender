using SuperRender.Browser.Networking;
using Xunit;

namespace SuperRender.Browser.Tests;

public class UrlResolverTests
{
    [Fact]
    public void Resolve_AbsoluteUrl_IgnoresBase()
    {
        var baseUri = new Uri("https://example.com/page");
        var result = UrlResolver.Resolve("https://other.com/path", baseUri);

        Assert.Equal("https://other.com/path", result.ToString());
    }

    [Fact]
    public void Resolve_RelativeUrl_WithBase_ResolvesCorrectly()
    {
        var baseUri = new Uri("https://example.com/dir/page.html");
        var result = UrlResolver.Resolve("other.html", baseUri);

        Assert.Equal(new Uri("https://example.com/dir/other.html"), result);
    }

    [Fact]
    public void Resolve_RelativeUrl_ParentPath()
    {
        var baseUri = new Uri("https://example.com/dir/sub/page.html");
        var result = UrlResolver.Resolve("../sibling.html", baseUri);

        Assert.Equal(new Uri("https://example.com/dir/sibling.html"), result);
    }

    [Fact]
    public void NormalizeAddress_Empty_ReturnsAboutBlank()
    {
        var result = UrlResolver.NormalizeAddress("");
        Assert.Equal(new Uri("about:blank"), result);
    }

    [Fact]
    public void NormalizeAddress_WhitespaceOnly_ReturnsAboutBlank()
    {
        var result = UrlResolver.NormalizeAddress("   ");
        Assert.Equal(new Uri("about:blank"), result);
    }

    [Fact]
    public void NormalizeAddress_WithScheme_Preserved()
    {
        var result = UrlResolver.NormalizeAddress("https://example.com");
        Assert.Equal("https", result.Scheme);
        Assert.Equal("example.com", result.Host);
    }

    [Fact]
    public void NormalizeAddress_BareDomain_GetsHttps()
    {
        var result = UrlResolver.NormalizeAddress("example.com");
        Assert.Equal("https", result.Scheme);
        Assert.Equal("example.com", result.Host);
    }

    [Fact]
    public void NormalizeAddress_AboutBlank_Preserved()
    {
        var result = UrlResolver.NormalizeAddress("about:blank");
        Assert.Equal("about:blank", result.ToString());
    }

    // ═══════════════════════════ file:// support ═══════════════════════════

    [Fact]
    public void NormalizeAddress_FileUri_Preserved()
    {
        var result = UrlResolver.NormalizeAddress("file:///Users/test/index.html");
        Assert.Equal("file", result.Scheme);
        Assert.Equal("/Users/test/index.html", result.LocalPath);
    }

    [Fact]
    public void NormalizeAddress_AbsoluteUnixPath_ConvertsToFileUri()
    {
        var result = UrlResolver.NormalizeAddress("/Users/test/index.html");
        Assert.Equal("file", result.Scheme);
        Assert.Contains("/Users/test/index.html", result.LocalPath);
    }

    [Fact]
    public void Resolve_RelativeToFileBase_ReturnsFileUri()
    {
        var baseUri = new Uri("file:///Users/test/pages/index.html");
        var result = UrlResolver.Resolve("style.css", baseUri);

        Assert.Equal("file", result.Scheme);
        Assert.Equal("/Users/test/pages/style.css", result.LocalPath);
    }

    [Fact]
    public void Resolve_ParentRelativeToFileBase_ReturnsFileUri()
    {
        var baseUri = new Uri("file:///Users/test/pages/index.html");
        var result = UrlResolver.Resolve("../shared/app.js", baseUri);

        Assert.Equal("file", result.Scheme);
        Assert.Equal("/Users/test/shared/app.js", result.LocalPath);
    }
}
