using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SuperRender.Browser.Networking;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;
using SuperRender.Gpu;

namespace SuperRender.Browser;

/// <summary>
/// Main browser window orchestrator. Owns the renderer, tab manager, chrome, and input handler.
/// </summary>
public sealed class BrowserWindow : IDisposable
{
    private readonly IWindow _window;
    private VulkanRenderer _renderer = null!;
    private TabManager _tabManager = null!;
    private BrowserChrome _chrome = null!;
    private InputHandler _inputHandler = null!;
    private ResourceLoader _resourceLoader = null!;
    private PaintList? _lastCombinedPaintList;
    private float _contentScale = 1.0f;

    public BrowserWindow(IWindow window)
    {
        _window = window;
    }

    public void OnLoad()
    {
        _renderer = new VulkanRenderer(_window);

        // HiDPI
        var logicalSize = _window.Size;
        var fbSize = _window.FramebufferSize;
        if (logicalSize.X > 0)
            _contentScale = (float)fbSize.X / logicalSize.X;
        _renderer.ContentScale = _contentScale;

        var measurer = new BitmapFontTextMeasurer(_renderer.FontAtlasData);
        _resourceLoader = new ResourceLoader();
        _tabManager = new TabManager(measurer, _resourceLoader);
        _chrome = new BrowserChrome();

        _inputHandler = new InputHandler(
            _chrome,
            _tabManager,
            () => _contentScale,
            NavigateAsync);

        // Create first tab with welcome page
        var tab = _tabManager.CreateTab();
        LoadWelcomePage(tab);

        // Input handling
        var input = _window.CreateInput();
        foreach (var kb in input.Keyboards)
        {
            kb.KeyDown += OnKeyDown;
            kb.KeyChar += OnKeyChar;
        }
        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
        }

        Console.WriteLine("SuperRenderer Browser started.");
    }

    public void OnRender(double deltaTime)
    {
        var fbSize = _window.FramebufferSize;
        if (fbSize.X == 0 || fbSize.Y == 0) return;

        float logicalWidth = fbSize.X / _contentScale;
        float logicalHeight = fbSize.Y / _contentScale;

        // Content area dimensions (below chrome)
        float contentWidth = logicalWidth;
        float contentHeight = logicalHeight - BrowserChrome.TotalChromeHeight;
        if (contentHeight < 0) contentHeight = 0;

        // Build chrome paint list
        var chromePaintList = _chrome.BuildPaintList(logicalWidth, _tabManager);

        // Build content paint list
        PaintList? contentPaintList = null;
        var activeTab = _tabManager.ActiveTab;
        if (activeTab is not null && contentHeight > 0)
        {
            contentPaintList = activeTab.Render(contentWidth, contentHeight);
        }

        // Combine: chrome + offset content
        var combined = new PaintList();
        foreach (var cmd in chromePaintList.Commands)
            combined.Add(cmd);

        if (contentPaintList is not null)
        {
            foreach (var cmd in contentPaintList.Commands)
            {
                combined.Add(OffsetCommand(cmd, BrowserChrome.TotalChromeHeight));
            }
        }

        _lastCombinedPaintList = combined;
        _renderer.RenderFrame(_lastCombinedPaintList);
    }

    public void OnResize(Vector2D<int> size)
    {
        _renderer.OnResize(size.X, size.Y);

        // Recalculate content scale
        var logicalSize = _window.Size;
        if (logicalSize.X > 0)
            _contentScale = (float)size.X / logicalSize.X;
        _renderer.ContentScale = _contentScale;

        // Mark active tab for re-layout
        _tabManager.ActiveTab?.MarkNeedsLayout();
    }

    public void OnClosing()
    {
        Dispose();
    }

    public void Dispose()
    {
        _tabManager?.Dispose();
        _resourceLoader?.Dispose();
        _renderer?.Dispose();
    }

    private void OnKeyDown(IKeyboard kb, Key key, int scancode)
    {
        _inputHandler.OnKeyDown(key);
    }

    private void OnKeyChar(IKeyboard kb, char c)
    {
        _inputHandler.OnCharInput(c);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left) return;
        var pos = mouse.Position;
        _inputHandler.OnMouseDown(pos.X * _contentScale, pos.Y * _contentScale, (float)_window.FramebufferSize.X);
    }

    private async void NavigateAsync(Uri uri)
    {
        _chrome.AddressText = uri.ToString();
        _chrome.CursorPosition = _chrome.AddressText.Length;
        await _tabManager.NavigateActiveTabAsync(uri).ConfigureAwait(false);
        _window.Title = $"{_tabManager.ActiveTab?.Title ?? "SuperRenderer"} - SuperRenderer Browser";
    }

    private static void LoadWelcomePage(Tab tab)
    {
        var welcomeHtml = """
            <html>
            <head>
                <style>
                    body {
                        background-color: #ffffff;
                        color: #333333;
                        font-size: 16px;
                        margin: 40px;
                    }
                    h1 {
                        color: #2c3e50;
                        font-size: 28px;
                        margin-bottom: 16px;
                    }
                    .welcome {
                        width: 600px;
                        padding: 24px;
                        background-color: #f8f9fa;
                        border-width: 1px;
                        border-color: #dee2e6;
                        border-style: solid;
                    }
                    p {
                        margin-bottom: 12px;
                        line-height: 1.5;
                    }
                    .hint {
                        color: #6c757d;
                        font-size: 14px;
                    }
                    ul {
                        margin-left: 20px;
                        margin-bottom: 12px;
                    }
                    li { margin-bottom: 6px; }
                </style>
            </head>
            <body>
                <h1>Welcome to SuperRenderer Browser</h1>
                <div class="welcome">
                    <p>This browser is powered by the SuperRenderer engine, built entirely in C# with Vulkan rendering.</p>
                    <p>Features:</p>
                    <ul>
                        <li>HTML + CSS rendering engine</li>
                        <li>ECMAScript 2025 JavaScript engine</li>
                        <li>Tab support</li>
                        <li>Cross-origin resource checking</li>
                        <li>HiDPI display support</li>
                    </ul>
                    <p class="hint">Type a URL in the address bar above and press Enter to navigate.</p>
                </div>
            </body>
            </html>
            """;

        // Use reflection-free approach: directly create pipeline
        var pipeline = typeof(Tab).GetField("_pipeline",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var measurer = typeof(Tab).GetField("_measurer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (pipeline is not null && measurer is not null)
        {
            var m = (ITextMeasurer)measurer.GetValue(tab)!;
            var rp = new SuperRender.Core.RenderPipeline(m, useUserAgentStylesheet: true);
            rp.LoadHtml(welcomeHtml);
            pipeline.SetValue(tab, rp);
        }
    }

    /// <summary>
    /// Offsets a paint command's Y coordinate by the given amount.
    /// </summary>
    private static PaintCommand OffsetCommand(PaintCommand cmd, float offsetY)
    {
        return cmd switch
        {
            FillRectCommand f => new FillRectCommand
            {
                Rect = new RectF(f.Rect.X, f.Rect.Y + offsetY, f.Rect.Width, f.Rect.Height),
                Color = f.Color,
            },
            StrokeRectCommand s => new StrokeRectCommand
            {
                Rect = new RectF(s.Rect.X, s.Rect.Y + offsetY, s.Rect.Width, s.Rect.Height),
                Color = s.Color,
                LineWidth = s.LineWidth,
            },
            DrawTextCommand t => new DrawTextCommand
            {
                Text = t.Text,
                X = t.X,
                Y = t.Y + offsetY,
                FontSize = t.FontSize,
                Color = t.Color,
            },
            _ => cmd,
        };
    }
}
