using SuperRender.Browser.Networking;
using SuperRender.Core;
using SuperRender.Core.Css;
using SuperRender.Core.Dom;
using SuperRender.Core.Painting;
using SuperRender.Core.Layout;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Interop;

namespace SuperRender.Browser;

/// <summary>
/// Represents a single browser tab with its own document, rendering pipeline, and JS engine.
/// </summary>
public sealed class Tab : IDisposable
{
    private readonly ITextMeasurer _measurer;
    private readonly ResourceLoader _loader;
    private RenderPipeline? _pipeline;
    private JsEngine? _jsEngine;
    private DomBridge? _domBridge;
    private PaintList? _lastPaintList;
    private bool _disposed;

    public string Title { get; private set; } = "New Tab";
    public Uri? CurrentUri { get; private set; }
    public Document? Document => _pipeline?.Document;
    public LayoutBox? LayoutRoot => _pipeline?.LayoutRoot;
    public bool IsLoading { get; private set; }
    public TextSelectionState Selection { get; } = new();

    // Scrolling
    public float ScrollOffsetY { get; set; }
    public float ContentHeight { get; private set; }

    // Navigation history
    private readonly List<Uri> _history = [];
    private int _historyIndex = -1;
    public bool CanGoBack => _historyIndex > 0;
    public bool CanGoForward => _historyIndex < _history.Count - 1;

    public Tab(ITextMeasurer measurer, ResourceLoader loader)
    {
        _measurer = measurer;
        _loader = loader;
    }

    /// <summary>
    /// Navigate this tab to a URL. Fetches HTML, parses, loads external CSS/JS with CORS checks,
    /// sets up JS engine with DOM bindings, and executes scripts.
    /// </summary>
    public async Task NavigateAsync(Uri uri, bool isHistoryNavigation = false)
    {
        IsLoading = true;
        CurrentUri = uri;
        Title = uri.Host;
        ScrollOffsetY = 0;

        // Update history (only for non-history navigations)
        if (!isHistoryNavigation)
        {
            // Truncate forward history
            if (_historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(uri);
            _historyIndex = _history.Count - 1;
        }

        try
        {
            // Handle about:blank
            if (uri.Scheme == "about")
            {
                LoadHtmlContent("<html><head></head><body></body></html>", uri);
                Title = "about:blank";
                return;
            }

            // Fetch HTML
            var result = await _loader.FetchHtmlAsync(uri).ConfigureAwait(false);
            if (result is null)
            {
                LoadErrorPage($"Failed to load {uri}");
                return;
            }

            var (html, finalUri) = result.Value;
            CurrentUri = finalUri;

            LoadHtmlContent(html, finalUri);

            // Fetch external CSS
            await LoadExternalStylesheetsAsync(finalUri).ConfigureAwait(false);

            // Set up JS engine
            SetupJsEngine();

            // Fetch and execute external scripts
            await LoadExternalScriptsAsync(finalUri).ConfigureAwait(false);

            // Execute inline scripts
            ExecuteInlineScripts();

            // Extract title
            var titleElement = FindElement(Document!, "title");
            if (titleElement is not null && !string.IsNullOrWhiteSpace(titleElement.InnerText))
                Title = titleElement.InnerText.Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tab] Navigation error: {ex.Message}");
            LoadErrorPage($"Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadHtmlContent(string html, Uri baseUri)
    {
        _pipeline = new RenderPipeline(_measurer, useUserAgentStylesheet: true);
        _pipeline.LoadHtml(html);
    }

    private void LoadErrorPage(string message)
    {
        var errorHtml = $$"""
            <html><head><style>
                body { margin: 40px; font-size: 16px; color: #333; }
                h1 { color: #c0392b; font-size: 24px; }
                p { margin-top: 16px; }
            </style></head>
            <body><h1>Navigation Error</h1><p>{{message}}</p></body></html>
            """;
        _pipeline = new RenderPipeline(_measurer, useUserAgentStylesheet: true);
        _pipeline.LoadHtml(errorHtml);
        Title = "Error";
    }

    private async Task LoadExternalStylesheetsAsync(Uri baseUri)
    {
        if (Document is null) return;

        var linkElements = FindElements(Document, "link");
        foreach (var link in linkElements)
        {
            var rel = link.GetAttribute("rel");
            var href = link.GetAttribute("href");
            if (rel is not null && rel.Equals("stylesheet", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(href))
            {
                var cssUri = UrlResolver.Resolve(href, baseUri);
                var cssText = await _loader.FetchCssAsync(cssUri, baseUri).ConfigureAwait(false);
                if (cssText is not null)
                {
                    var cssParser = new CssParser(cssText);
                    var stylesheet = cssParser.Parse();
                    Document.Stylesheets.Add(stylesheet);
                }
            }
        }
    }

    private async Task LoadExternalScriptsAsync(Uri baseUri)
    {
        if (Document is null || _jsEngine is null) return;

        var scriptElements = FindElements(Document, "script");
        foreach (var script in scriptElements)
        {
            var src = script.GetAttribute("src");
            if (!string.IsNullOrWhiteSpace(src))
            {
                var jsUri = UrlResolver.Resolve(src, baseUri);
                var jsCode = await _loader.FetchJsAsync(jsUri, baseUri).ConfigureAwait(false);
                if (jsCode is not null)
                {
                    try { _jsEngine.Execute(jsCode); }
                    catch (Exception ex) { Console.WriteLine($"[JS] Script error ({src}): {ex.Message}"); }
                }
            }
        }
    }

    private void ExecuteInlineScripts()
    {
        if (Document is null || _jsEngine is null) return;

        var scriptElements = FindElements(Document, "script");
        foreach (var script in scriptElements)
        {
            var src = script.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(src) && !string.IsNullOrWhiteSpace(script.InnerText))
            {
                try { _jsEngine.Execute(script.InnerText); }
                catch (Exception ex) { Console.WriteLine($"[JS] Inline script error: {ex.Message}"); }
            }
        }
    }

    private void SetupJsEngine()
    {
        if (Document is null) return;

        _jsEngine = new JsEngine();
        _domBridge = new DomBridge(_jsEngine, Document);
        _domBridge.Install();
    }

    public PaintList? Render(float width, float height)
    {
        if (_pipeline is null) return null;

        var paintList = _pipeline.RenderIfDirty(width, height);
        if (paintList is not null)
            _lastPaintList = paintList;

        _lastPaintList ??= _pipeline.Render(width, height);

        // Update content height from layout root
        if (_pipeline.LayoutRoot is not null)
            ContentHeight = _pipeline.LayoutRoot.Dimensions.MarginRect.Bottom;

        return _lastPaintList;
    }

    /// <summary>
    /// Go back in navigation history. Returns the URI to navigate to, or null if can't go back.
    /// </summary>
    public Uri? GoBack()
    {
        if (!CanGoBack) return null;
        _historyIndex--;
        return _history[_historyIndex];
    }

    /// <summary>
    /// Go forward in navigation history. Returns the URI to navigate to, or null if can't go forward.
    /// </summary>
    public Uri? GoForward()
    {
        if (!CanGoForward) return null;
        _historyIndex++;
        return _history[_historyIndex];
    }

    public void MarkNeedsLayout()
    {
        if (_pipeline?.Document is not null)
            _pipeline.Document.NeedsLayout = true;
    }

    private static Element? FindElement(Node root, string tagName)
    {
        if (root is Element el && el.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
            return el;
        foreach (var child in root.Children)
        {
            var found = FindElement(child, tagName);
            if (found is not null) return found;
        }
        return null;
    }

    private static List<Element> FindElements(Node root, string tagName)
    {
        var results = new List<Element>();
        CollectElements(root, tagName, results);
        return results;
    }

    private static void CollectElements(Node node, string tagName, List<Element> results)
    {
        if (node is Element el && el.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
            results.Add(el);
        foreach (var child in node.Children)
            CollectElements(child, tagName, results);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
