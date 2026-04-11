using SuperRender.Renderer.Rendering;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.Browser.Tests;

public class LocationHistoryTests
{
    private static (JsEngine engine, DomBridge bridge, Uri[] navigatedUris, Uri[] replacedUris) CreateTestEnvironment(
        string currentUrl = "https://example.com/page")
    {
        var pipeline = new RenderPipeline(new SuperRender.Renderer.Rendering.Layout.MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml("<html><body></body></html>");
        var engine = new JsEngine();
        var bridge = new DomBridge(engine, doc);

        var currentUri = new Uri(currentUrl);
        var navigated = new List<Uri>();
        var replaced = new List<Uri>();
        var addressBarUri = currentUri;

        bridge.SetLocationAndHistory(
            () => addressBarUri,
            url =>
            {
                var resolved = new Uri(addressBarUri, url);
                navigated.Add(resolved);
                addressBarUri = resolved;
            },
            url =>
            {
                var resolved = new Uri(addressBarUri, url);
                replaced.Add(resolved);
                addressBarUri = resolved;
            },
            () => { /* reload - no-op for tests */ },
            () => { /* goBack */ },
            () => { /* goForward */ },
            uri => { addressBarUri = uri; });

        bridge.Install();
        return (engine, bridge, navigated.ToArray(), replaced.ToArray());
    }

    // Using a captured uri list won't work since we capture at creation time.
    // Let me use a different approach with mutable captures.

    private static (JsEngine engine, DomBridge bridge, List<Uri> navigatedUris, List<Uri> replacedUris) CreateMutableTestEnvironment(
        string currentUrl = "https://example.com/page")
    {
        var pipeline = new RenderPipeline(new SuperRender.Renderer.Rendering.Layout.MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml("<html><body></body></html>");
        var engine = new JsEngine();
        var bridge = new DomBridge(engine, doc);

        var currentUri = new Uri(currentUrl);
        var navigated = new List<Uri>();
        var replaced = new List<Uri>();
        // Use a wrapper so the closure captures the reference correctly
        var wrapper = new UriWrapper { Uri = currentUri };

        bridge.SetLocationAndHistory(
            () => wrapper.Uri,
            url =>
            {
                var resolved = new Uri(wrapper.Uri, url);
                navigated.Add(resolved);
                wrapper.Uri = resolved;
            },
            url =>
            {
                var resolved = new Uri(wrapper.Uri, url);
                replaced.Add(resolved);
                wrapper.Uri = resolved;
            },
            () => { /* reload */ },
            () => { /* goBack */ },
            () => { /* goForward */ },
            uri => { wrapper.Uri = uri; });

        bridge.Install();
        return (engine, bridge, navigated, replaced);
    }

    private sealed class UriWrapper
    {
        public Uri Uri { get; set; } = new("about:blank");
    }

    // --- window.location tests ---

    [Fact]
    public void Location_Href_ReturnsCurrentUrl()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.href");
        Assert.Equal("https://example.com/page", result.ToJsString());
    }

    [Fact]
    public void Location_Protocol_ReturnsSchemeWithColon()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.protocol");
        Assert.Equal("https:", result.ToJsString());
    }

    [Fact]
    public void Location_Host_ReturnsHostname()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.host");
        Assert.Equal("example.com", result.ToJsString());
    }

    [Fact]
    public void Location_Hostname_ReturnsHost()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.hostname");
        Assert.Equal("example.com", result.ToJsString());
    }

    [Fact]
    public void Location_Pathname_ReturnsPath()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.pathname");
        Assert.Equal("/page", result.ToJsString());
    }

    [Fact]
    public void Location_Search_ReturnsQueryString()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment("https://example.com/page?q=test");
        var result = engine.Execute("window.location.search");
        Assert.Equal("?q=test", result.ToJsString());
    }

    [Fact]
    public void Location_Hash_ReturnsFragment()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment("https://example.com/page#section");
        var result = engine.Execute("window.location.hash");
        Assert.Equal("#section", result.ToJsString());
    }

    [Fact]
    public void Location_Origin_ReturnsSchemeHostPort()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.origin");
        Assert.Equal("https://example.com", result.ToJsString());
    }

    [Fact]
    public void Location_Assign_Navigates()
    {
        var (engine, _, navigated, _) = CreateMutableTestEnvironment();
        engine.Execute("window.location.assign('https://other.com/')");
        Assert.Single(navigated);
        Assert.Equal("https://other.com/", navigated[0].ToString());
    }

    [Fact]
    public void Location_Replace_NavigatesWithReplace()
    {
        var (engine, _, _, replaced) = CreateMutableTestEnvironment();
        engine.Execute("window.location.replace('https://other.com/')");
        Assert.Single(replaced);
    }

    [Fact]
    public void Location_SetHref_Navigates()
    {
        var (engine, _, navigated, _) = CreateMutableTestEnvironment();
        engine.Execute("window.location.href = 'https://new.com/'");
        Assert.Single(navigated);
    }

    [Fact]
    public void Location_ToString_ReturnsHref()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.location.toString()");
        Assert.Equal("https://example.com/page", result.ToJsString());
    }

    [Fact]
    public void Location_GlobalAccess()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("location.href");
        Assert.Equal("https://example.com/page", result.ToJsString());
    }

    // --- window.history tests ---

    [Fact]
    public void History_Length_ReturnsCount()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var result = engine.Execute("window.history.length");
        Assert.True(result.ToNumber() >= 1);
    }

    [Fact]
    public void History_PushState_IncreasesLength()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var before = engine.Execute("window.history.length").ToNumber();
        engine.Execute("window.history.pushState(null, '', '/new')");
        var after = engine.Execute("window.history.length").ToNumber();
        Assert.Equal(before + 1, after);
    }

    [Fact]
    public void History_PushState_UpdatesLocationPathname()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        engine.Execute("window.history.pushState(null, '', '/newpath')");
        var result = engine.Execute("window.location.pathname");
        Assert.Equal("/newpath", result.ToJsString());
    }

    [Fact]
    public void History_PushState_PreservesState()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        engine.Execute("window.history.pushState({key: 'value'}, '', '/new')");
        var result = engine.Execute("window.history.state.key");
        Assert.Equal("value", result.ToJsString());
    }

    [Fact]
    public void History_ReplaceState_DoesNotIncreaseLength()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        var before = engine.Execute("window.history.length").ToNumber();
        engine.Execute("window.history.replaceState(null, '', '/replaced')");
        var after = engine.Execute("window.history.length").ToNumber();
        Assert.Equal(before, after);
    }

    [Fact]
    public void History_ReplaceState_UpdatesState()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        engine.Execute("window.history.replaceState({x: 42}, '', '/same')");
        var result = engine.Execute("window.history.state.x");
        Assert.Equal(42.0, result.ToNumber());
    }

    [Fact]
    public void History_Back_NavigatesBackward()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        engine.Execute("window.history.pushState(null, '', '/page2')");
        engine.Execute("window.history.pushState(null, '', '/page3')");
        engine.Execute("window.history.back()");
        var result = engine.Execute("window.location.pathname");
        Assert.Equal("/page2", result.ToJsString());
    }

    [Fact]
    public void History_Forward_NavigatesForward()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        engine.Execute("window.history.pushState(null, '', '/page2')");
        engine.Execute("window.history.back()");
        engine.Execute("window.history.forward()");
        var result = engine.Execute("window.location.pathname");
        Assert.Equal("/page2", result.ToJsString());
    }

    [Fact]
    public void History_Go_NavigatesByDelta()
    {
        var (engine, _, _, _) = CreateMutableTestEnvironment();
        engine.Execute("window.history.pushState(null, '', '/p2')");
        engine.Execute("window.history.pushState(null, '', '/p3')");
        engine.Execute("window.history.go(-2)");
        var result = engine.Execute("window.location.pathname");
        Assert.Equal("/page", result.ToJsString());
    }
}
