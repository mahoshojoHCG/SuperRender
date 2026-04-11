using System.Collections.Concurrent;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SuperRender.Browser.Networking;
using SuperRender.Document;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Painting;
using SuperRender.Renderer.Gpu;

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
    private DevToolsWindow? _devToolsWindow;
    private PaintList? _lastCombinedPaintList;
    private ContextMenu? _contextMenu;
    private ITextMeasurer _measurer = null!;
    private float _contentScale = 1.0f;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

    public BrowserWindow(IWindow window)
    {
        _window = window;
    }

    public void OnLoad()
    {
        // HiDPI: compute content scale before creating renderer so font atlas
        // can be generated at the correct resolution.
        var logicalSize = _window.Size;
        var fbSize = _window.FramebufferSize;
        if (logicalSize.X > 0)
            _contentScale = (float)fbSize.X / logicalSize.X;

        _renderer = new VulkanRenderer(_window, _contentScale);

        var measurer = new BitmapFontTextMeasurer(_renderer.FontAtlasData);
        _measurer = measurer;
        _resourceLoader = new ResourceLoader();
        _tabManager = new TabManager(measurer, _resourceLoader);
        _chrome = new BrowserChrome(measurer);

        _inputHandler = new InputHandler(
            _chrome,
            _tabManager,
            measurer,
            () => _contentScale,
            NavigateAsync,
            GoBackAsync,
            GoForwardAsync,
            ToggleDevTools);

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
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnMouseScroll;
        }

        Console.WriteLine("SuperRenderer Browser started.");
    }

    public void OnRender(double deltaTime)
    {
        // Drain pending main-thread work
        while (_mainThreadQueue.TryDequeue(out var action))
            action();

        // Drain JS timers (before rendering so DOM changes take effect this frame)
        _tabManager.ActiveTab?.Timers?.DrainReady();

        // Drain DevTools execution queue
        DrainDevToolsExecutionQueue();

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

        // Combine: content first, then chrome on top (so chrome always covers any content bleed)
        var combined = new PaintList();

        if (contentPaintList is not null)
        {
            float scrollY = activeTab?.Scroll.ScrollY ?? 0;
            float contentOffset = BrowserChrome.TotalChromeHeight - scrollY;

            // Two-pass rendering: quads first, then selection highlights, then text.
            // This ensures selection is always between backgrounds and text even when
            // the content has nested clip segments (overflow:hidden, positioned elements).

            // Pass 1: content quads (backgrounds, borders) with clip structure
            combined.Add(new PushClipCommand
            {
                Rect = new RectF(0, BrowserChrome.TotalChromeHeight, logicalWidth, contentHeight),
            });
            foreach (var cmd in contentPaintList.Commands)
            {
                var offset = OffsetCommand(cmd, contentOffset);
                if (offset is not DrawTextCommand)
                    combined.Add(offset);
            }
            combined.Add(new PopClipCommand());

            // Selection highlights (between backgrounds and text)
            if (activeTab?.Selection.HasSelection == true && activeTab.LayoutRoot is not null)
            {
                combined.Add(new PushClipCommand
                {
                    Rect = new RectF(0, BrowserChrome.TotalChromeHeight, logicalWidth, contentHeight),
                });
                var allRuns = TextHitTester.CollectTextRuns(activeTab.LayoutRoot);
                var highlights = SelectionPainter.BuildHighlights(activeTab.Selection, allRuns, _measurer);
                foreach (var cmd in highlights.Commands)
                    combined.Add(OffsetCommand(cmd, contentOffset));
                combined.Add(new PopClipCommand());
            }

            // Pass 2: content text with clip structure
            combined.Add(new PushClipCommand
            {
                Rect = new RectF(0, BrowserChrome.TotalChromeHeight, logicalWidth, contentHeight),
            });
            foreach (var cmd in contentPaintList.Commands)
            {
                var offset = OffsetCommand(cmd, contentOffset);
                if (offset is DrawTextCommand or PushClipCommand or PopClipCommand)
                    combined.Add(offset);
            }
            combined.Add(new PopClipCommand());

            // Scrollbar (outside clip so it renders over chrome border)
            var scrollGeo = activeTab?.Scroll.GetScrollBarGeometry(BrowserChrome.TotalChromeHeight);
            if (scrollGeo.HasValue)
            {
                var (trackY, trackHeight, thumbY, thumbHeight) = scrollGeo.Value;
                float barX = logicalWidth - ScrollState.BarWidth;

                // Track background (opaque light gray)
                combined.Add(new FillRectCommand
                {
                    Rect = new RectF(barX, trackY, ScrollState.BarWidth, trackHeight),
                    Color = Color.FromRgb(240, 240, 240),
                });

                // Thumb (opaque medium gray)
                combined.Add(new FillRectCommand
                {
                    Rect = new RectF(barX + 1, thumbY, ScrollState.BarWidth - 2, thumbHeight),
                    Color = Color.FromRgb(180, 180, 180),
                });
            }
        }

        // Chrome on top of content — in its own segment so chrome text stays above content
        combined.Add(new PushClipCommand
        {
            Rect = new RectF(0, 0, logicalWidth, BrowserChrome.TotalChromeHeight),
        });
        foreach (var cmd in chromePaintList.Commands)
            combined.Add(cmd);
        combined.Add(new PopClipCommand());

        _lastCombinedPaintList = combined;

        // Context menu renders on top of everything — wrapped in its own clip segment
        // so quads and text are rendered together, above all prior content
        if (_contextMenu is { IsVisible: true })
        {
            combined.Add(new PushClipCommand
            {
                Rect = new RectF(0, 0, logicalWidth, logicalHeight),
            });
            var menuPaintList = _contextMenu.BuildPaintList();
            foreach (var cmd in menuPaintList.Commands)
                combined.Add(cmd);
            combined.Add(new PopClipCommand());
        }

        _renderer.RenderFrame(_lastCombinedPaintList);

        // Drive the DevTools window rendering (secondary windows aren't driven by Silk.NET's Run())
        _devToolsWindow?.RenderFrame();
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
        _devToolsWindow?.Dispose();
        _tabManager?.Dispose();
        _resourceLoader?.Dispose();
        _renderer?.Dispose();
    }

    private void OnKeyDown(IKeyboard kb, Key key, int scancode)
    {
        _inputHandler.OnKeyDown(key, kb);
    }

    private void OnKeyChar(IKeyboard kb, char c)
    {
        _inputHandler.OnCharInput(c);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        var pos = mouse.Position;
        float physX = pos.X * _contentScale;
        float physY = pos.Y * _contentScale;
        float scale = _contentScale;
        float logX = physX / scale;
        float logY = physY / scale;

        // Dismiss open context menu on any click
        if (_contextMenu is { IsVisible: true })
        {
            if (button == MouseButton.Left)
            {
                int idx = _contextMenu.HitTest(logX, logY);
                if (idx >= 0 && _contextMenu.Items[idx].Enabled)
                    _contextMenu.Items[idx].Action();
            }
            _contextMenu = null;
            return;
        }

        if (button == MouseButton.Left)
        {
            _inputHandler.OnMouseDown(physX, physY, (float)_window.FramebufferSize.X);
        }
        else if (button == MouseButton.Right)
        {
            _contextMenu = _inputHandler.OnRightClick(physX, physY, (float)_window.FramebufferSize.X);
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left) return;
        var pos = mouse.Position;
        _inputHandler.OnMouseUp(pos.X * _contentScale, pos.Y * _contentScale, (float)_window.FramebufferSize.X);
    }

    private void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        float scale = _contentScale;
        float logX = position.X;
        float logY = position.Y;

        // Update context menu hover
        if (_contextMenu is { IsVisible: true })
        {
            _contextMenu.UpdateHover(logX, logY);
        }

        float physX = position.X * scale;
        float physY = position.Y * scale;
        _inputHandler.OnMouseMove(physX, physY, (float)_window.FramebufferSize.X);
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _inputHandler.OnScroll(wheel.Y);
    }

    private async void NavigateAsync(Uri uri)
    {
        _chrome.AddressText = uri.ToString();
        _chrome.CursorPosition = _chrome.AddressText.Length;
        await _tabManager.NavigateActiveTabAsync(uri).ConfigureAwait(false);
        _mainThreadQueue.Enqueue(() =>
        {
            _window.Title = $"{_tabManager.ActiveTab?.Title ?? "SuperRenderer"} - SuperRenderer Browser";
        });
    }

    private async void GoBackAsync()
    {
        var tab = _tabManager.ActiveTab;
        if (tab is null || !tab.History.CanGoBack) return;
        await tab.GoBackAsync().ConfigureAwait(false);
        _mainThreadQueue.Enqueue(() =>
        {
            _chrome.AddressText = tab.CurrentUri?.ToString() ?? "";
            _chrome.CursorPosition = _chrome.AddressText.Length;
            _window.Title = $"{tab.Title} - SuperRenderer Browser";
        });
    }

    private async void GoForwardAsync()
    {
        var tab = _tabManager.ActiveTab;
        if (tab is null || !tab.History.CanGoForward) return;
        await tab.GoForwardAsync().ConfigureAwait(false);
        _mainThreadQueue.Enqueue(() =>
        {
            _chrome.AddressText = tab.CurrentUri?.ToString() ?? "";
            _chrome.CursorPosition = _chrome.AddressText.Length;
            _window.Title = $"{tab.Title} - SuperRenderer Browser";
        });
    }

    private void ToggleDevTools()
    {
        if (_devToolsWindow is { IsOpen: true })
        {
            _devToolsWindow.Close();
            _devToolsWindow = null;
        }
        else
        {
            _devToolsWindow = new DevToolsWindow(
                () => _tabManager.ActiveTab?.ConsoleLog,
                () => _devToolsWindow = null);
            _devToolsWindow.Open();
        }
    }

    private void DrainDevToolsExecutionQueue()
    {
        if (_devToolsWindow is null) return;

        while (_devToolsWindow.ExecutionQueue.TryDequeue(out var code))
        {
            var tab = _tabManager.ActiveTab;
            if (tab is null) continue;

            var result = tab.ExecuteConsoleInput(code);
            if (result is not null)
            {
                tab.ConsoleLog.Add(new ConsoleMessage
                {
                    Level = ConsoleMessageLevel.Info,
                    Text = "< " + result,
                });
            }
        }
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

        tab.LoadHtmlDirect(welcomeHtml);
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
                FontWeight = t.FontWeight,
                FontStyle = t.FontStyle,
                FontFamily = t.FontFamily,
                FontFamilies = t.FontFamilies,
            },
            PushClipCommand c => new PushClipCommand
            {
                Rect = new RectF(c.Rect.X, c.Rect.Y + offsetY, c.Rect.Width, c.Rect.Height),
            },
            _ => cmd,
        };
    }
}
