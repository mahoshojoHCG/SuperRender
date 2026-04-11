using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Browser.Networking;
using SuperRender.Browser.Storage;
using SuperRender.Renderer.Rendering;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Painting;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.EcmaScript.Runtime.Builtins;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.Runtime;

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
    public DomDocument? Document => _pipeline?.Document;
    public LayoutBox? LayoutRoot => _pipeline?.LayoutRoot;
    public bool IsLoading { get; private set; }
    public TextSelectionState Selection { get; } = new();
    public ScrollState Scroll { get; } = new();
    public NavigationHistory History { get; } = new();
    public ConsoleLog ConsoleLog { get; } = new();
    public TimerScheduler? Timers => _domBridge?.TimerQueue;
    public SessionStorage SessionStorage { get; } = new();

    // External dependencies injected from BrowserWindow
    public CookieJar? CookieJar { get; set; }
    public StorageDatabase? StorageDb { get; set; }
    public Action<Action>? EnqueueMainThread { get; set; }
    public ImageCache? ImageCache { get; set; }

    public Tab(ITextMeasurer measurer, ResourceLoader loader)
    {
        _measurer = measurer;
        _loader = loader;
    }

    /// <summary>
    /// Loads HTML directly into this tab without network fetch.
    /// Sets up the rendering pipeline and JS engine.
    /// Used for welcome page, view-source, and other locally-generated content.
    /// </summary>
    public void LoadHtmlDirect(string html)
    {
        _pipeline = new RenderPipeline(_measurer, useUserAgentStylesheet: true);
        _pipeline.LoadHtml(html);
        SetupJsEngine();
    }

    /// <summary>
    /// Navigate this tab to a URL. Pushes to history stack.
    /// Fetches HTML, parses, loads external CSS/JS with CORS checks,
    /// sets up JS engine with DOM bindings, and executes scripts.
    /// </summary>
    public async Task NavigateAsync(Uri uri)
    {
        History.Push(uri);
        await NavigateInternalAsync(uri).ConfigureAwait(false);
    }

    /// <summary>
    /// Navigate to the previous history entry.
    /// </summary>
    public async Task GoBackAsync()
    {
        var uri = History.GoBack();
        if (uri is not null)
            await NavigateInternalAsync(uri).ConfigureAwait(false);
    }

    /// <summary>
    /// Navigate to the next history entry.
    /// </summary>
    public async Task GoForwardAsync()
    {
        var uri = History.GoForward();
        if (uri is not null)
            await NavigateInternalAsync(uri).ConfigureAwait(false);
    }

    private async Task NavigateInternalAsync(Uri uri)
    {
        IsLoading = true;
        CurrentUri = uri;
        Title = uri.Scheme == "sr" ? uri.AbsolutePath.TrimStart('/') : uri.Host;
        Scroll.ScrollToTop();

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

            // Fetch and decode images
            await LoadImagesAsync(finalUri).ConfigureAwait(false);

            // Set up JS engine
            SetupJsEngine();

            // Fetch and execute external scripts
            await LoadExternalScriptsAsync(finalUri).ConfigureAwait(false);

            // Execute inline scripts
            ExecuteInlineScripts();

            // Fire DOMContentLoaded on document
            Document?.DispatchEvent(new DomEvent
            {
                Type = "DOMContentLoaded", Bubbles = true, Cancelable = false,
            });

            // Extract title
            var titleElement = FindElement(Document!, "title");
            if (titleElement is not null && !string.IsNullOrWhiteSpace(titleElement.InnerText))
                Title = titleElement.InnerText.Trim();

            // Fire load event on document (after all resources loaded)
            Document?.DispatchEvent(new DomEvent
            {
                Type = "load", Bubbles = false, Cancelable = false,
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tab] Navigation error: {ex.Message}");
            ConsoleLog.Add(new ConsoleMessage
            {
                Level = ConsoleMessageLevel.Error,
                Text = $"Navigation error: {ex.Message}",
            });
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

    private async Task LoadImagesAsync(Uri baseUri)
    {
        if (Document is null || ImageCache is null) return;

        var imgElements = FindElements(Document, "img");
        foreach (var img in imgElements)
        {
            var src = img.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(src)) continue;

            var imgUri = UrlResolver.Resolve(src, baseUri);
            var urlKey = imgUri.ToString();

            if (ImageCache.Contains(urlKey))
            {
                ApplyImageDimensions(img, ImageCache.Get(urlKey));
                continue;
            }

            var bytes = await _loader.FetchImageBytesAsync(imgUri, baseUri).ConfigureAwait(false);
            if (bytes is not null)
            {
                var imageData = Renderer.Image.ImageDecoder.Decode(bytes);
                ImageCache.Set(urlKey, imageData);
                ApplyImageDimensions(img, imageData);
            }
            else
            {
                ImageCache.Set(urlKey, null);
            }
        }
    }

    private static void ApplyImageDimensions(Element img, Renderer.Image.ImageData? imageData)
    {
        if (imageData is null) return;

        img.SetAttribute("data-natural-width",
            imageData.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        img.SetAttribute("data-natural-height",
            imageData.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JS] Script error ({src}): {ex.Message}");
                        ConsoleLog.Add(new ConsoleMessage
                        {
                            Level = ConsoleMessageLevel.Error,
                            Text = $"Script error ({src}): {ex.Message}",
                        });
                    }
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
                catch (Exception ex)
                {
                    Console.WriteLine($"[JS] Inline script error: {ex.Message}");
                    ConsoleLog.Add(new ConsoleMessage
                    {
                        Level = ConsoleMessageLevel.Error,
                        Text = $"Inline script error: {ex.Message}",
                    });
                }
            }
        }
    }

    private void SetupJsEngine()
    {
        if (Document is null) return;

        _jsEngine = new JsEngine();

        // Redirect console output to the DevTools console log
        var stdCapture = new ConsoleCapture(ConsoleLog, ConsoleMessageLevel.Log);
        var errCapture = new ConsoleCapture(ConsoleLog, ConsoleMessageLevel.Error);
        var warnCapture = new ConsoleCapture(ConsoleLog, ConsoleMessageLevel.Warn);
        _jsEngine.SetConsoleOutput(stdCapture, errCapture, warnCapture);

        _domBridge = new DomBridge(_jsEngine, Document);

        // Wire up storage
        if (StorageDb is not null && CurrentUri is not null)
        {
            var origin = GetOrigin(CurrentUri);
            var localStorage = new LocalStorage(StorageDb, origin);
            _domBridge.SetLocalStorage(
                localStorage.GetItem, localStorage.SetItem, localStorage.RemoveItem,
                localStorage.Clear, localStorage.Key, () => localStorage.Length);
        }

        _domBridge.SetSessionStorage(
            SessionStorage.GetItem, SessionStorage.SetItem, SessionStorage.RemoveItem,
            SessionStorage.Clear, SessionStorage.Key, () => SessionStorage.Length);

        // Wire up cookies
        if (CookieJar is not null && CurrentUri is not null)
        {
            var origin = CurrentUri;
            _domBridge.SetCookies(
                () => CookieJar.GetCookiesForScript(origin),
                cookieStr => CookieJar.SetCookieFromScript(origin, cookieStr));
        }

        // Wire up fetch API
        if (EnqueueMainThread is not null)
        {
            _domBridge.SetFetch(
                async (url, method, headers, body) =>
                {
                    var baseUri = CurrentUri ?? new Uri("about:blank");
                    var fetchUri = UrlResolver.Resolve(url, baseUri);
                    var response = await _loader.FetchGenericAsync(fetchUri, method, headers, body).ConfigureAwait(false);
                    return new FetchResult
                    {
                        Status = response.Status,
                        StatusText = response.StatusText,
                        Body = response.Body,
                        Url = response.Url,
                        Headers = response.Headers,
                    };
                },
                EnqueueMainThread);
        }

        // Wire up location and history
        _domBridge.SetLocationAndHistory(
            () => CurrentUri,
            url =>
            {
                var baseUri = CurrentUri ?? new Uri("about:blank");
                var newUri = UrlResolver.Resolve(url, baseUri);
                EnqueueMainThread?.Invoke(() => _ = NavigateAsync(newUri));
            },
            url =>
            {
                var baseUri = CurrentUri ?? new Uri("about:blank");
                var newUri = UrlResolver.Resolve(url, baseUri);
                if (CurrentUri is not null)
                    History.ReplaceCurrent(newUri);
                EnqueueMainThread?.Invoke(() => _ = NavigateInternalAsync(newUri));
            },
            () => EnqueueMainThread?.Invoke(() => { if (CurrentUri is not null) _ = NavigateInternalAsync(CurrentUri); }),
            () => EnqueueMainThread?.Invoke(() => _ = GoBackAsync()),
            () => EnqueueMainThread?.Invoke(() => _ = GoForwardAsync()),
            uri => { CurrentUri = uri; });

        _domBridge.Install();
    }

    /// <summary>
    /// Executes a JavaScript expression from the DevTools console input.
    /// Returns a formatted result string, or null if no engine is available.
    /// Errors are logged to ConsoleLog automatically.
    /// </summary>
    public string? ExecuteConsoleInput(string code)
    {
        // Lazy-init JS engine if document exists but engine wasn't set up
        if (_jsEngine is null && Document is not null)
            SetupJsEngine();

        if (_jsEngine is null)
        {
            ConsoleLog.Add(new ConsoleMessage
            {
                Level = ConsoleMessageLevel.Error,
                Text = "JavaScript engine not available",
            });
            return null;
        }

        try
        {
            var result = _jsEngine.Execute(code);
            if (result is JsUndefined)
                return "undefined";
            return ConsoleObject.FormatForDisplay(result);
        }
        catch (Exception ex)
        {
            ConsoleLog.Add(new ConsoleMessage
            {
                Level = ConsoleMessageLevel.Error,
                Text = ex.Message,
            });
            return null;
        }
    }

    public PaintList? Render(float width, float height)
    {
        if (_pipeline is null) return null;

        var paintList = _pipeline.RenderIfDirty(width, height);
        if (paintList is not null)
            _lastPaintList = paintList;

        _lastPaintList ??= _pipeline.Render(width, height);

        // Update scroll bounds from layout
        if (_pipeline.LayoutRoot is not null)
        {
            var rootDims = _pipeline.LayoutRoot.Dimensions;
            float contentBottom = rootDims.MarginRect.Bottom;
            Scroll.Update(contentBottom, height);
        }

        return _lastPaintList;
    }

    public void MarkNeedsLayout()
    {
        if (_pipeline?.Document is not null)
            _pipeline.Document.NeedsLayout = true;
    }

    private static string GetOrigin(Uri uri)
    {
        if (uri.Scheme is "file" or "sr" or "about")
            return uri.Scheme + "://";
        return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
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
