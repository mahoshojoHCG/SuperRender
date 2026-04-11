using System.Collections.Concurrent;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Gpu;

namespace SuperRender.Browser;

/// <summary>
/// A separate window that displays the developer tools console.
/// Owns its own Silk.NET window and Vulkan renderer.
/// </summary>
public sealed class DevToolsWindow : IDisposable
{
    private IWindow? _window;
    private VulkanRenderer? _renderer;
    private DevToolsPanel? _panel;
    private ITextMeasurer? _measurer;
    private IInputContext? _inputContext;
    private float _contentScale = 1.0f;
    private bool _disposed;

    private readonly Func<ConsoleLog?> _getConsoleLog;
    private readonly Action _onClosed;

    /// <summary>
    /// Queue of JS code strings to execute on the active tab's engine.
    /// Drained by BrowserWindow on the main thread.
    /// </summary>
    public ConcurrentQueue<string> ExecutionQueue { get; } = new();

    public bool IsOpen => _window is not null && !_disposed;

    public DevToolsWindow(Func<ConsoleLog?> getConsoleLog, Action onClosed)
    {
        _getConsoleLog = getConsoleLog;
        _onClosed = onClosed;
    }

    /// <summary>
    /// Creates and shows the DevTools window. Must be called on the main thread.
    /// </summary>
    public void Open()
    {
        if (_window is not null) return;

        _disposed = false;

        var opts = WindowOptions.DefaultVulkan with
        {
            Size = new Vector2D<int>(700, 500),
            Title = "Developer Tools - SuperRenderer",
            IsEventDriven = false,
        };

        _window = Window.Create(opts);
        _window.Load += OnLoad;
        _window.FramebufferResize += OnResize;
        _window.Initialize();
    }

    /// <summary>
    /// Marks the DevTools window for closing. Actual cleanup happens in RenderFrame().
    /// </summary>
    public void Close()
    {
        if (_window is null) return;
        _window.IsClosing = true;
    }

    /// <summary>
    /// Called each frame by BrowserWindow to render the DevTools window.
    /// Silk.NET only drives the render loop for the window that called Run(),
    /// so secondary windows must be rendered manually from the main loop.
    /// GLFW's glfwPollEvents() (called by the main Run() loop) handles input
    /// events for all windows, so keyboard/mouse callbacks still fire.
    /// </summary>
    public void RenderFrame()
    {
        if (_window is null || _renderer is null || _panel is null) return;

        // Detect OS close button click or Close() call
        if (_window.IsClosing)
        {
            DisposeResources();
            _onClosed();
            return;
        }

        var fbSize = _window.FramebufferSize;
        if (fbSize.X == 0 || fbSize.Y == 0) return;

        float logicalWidth = fbSize.X / _contentScale;
        float logicalHeight = fbSize.Y / _contentScale;

        // Update panel height to match window
        _panel.Height = logicalHeight;

        var log = _getConsoleLog();
        if (log is null) return;

        var paintList = _panel.BuildPaintList(logicalWidth, 0, log);
        _renderer.RenderFrame(paintList);
    }

    private void OnLoad()
    {
        if (_window is null) return;

        var logicalSize = _window.Size;
        var fbSize = _window.FramebufferSize;
        if (logicalSize.X > 0)
            _contentScale = (float)fbSize.X / logicalSize.X;

        _renderer = new VulkanRenderer(_window, _contentScale);
        _measurer = new BitmapFontTextMeasurer(_renderer.FontAtlasData);
        _panel = new DevToolsPanel(_measurer);
        _panel.InputFocused = true;

        // Input handling
        _inputContext = _window.CreateInput();
        foreach (var kb in _inputContext.Keyboards)
        {
            kb.KeyDown += OnKeyDown;
            kb.KeyChar += OnKeyChar;
        }
        foreach (var mouse in _inputContext.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.Scroll += OnMouseScroll;
        }
    }

    private void OnResize(Vector2D<int> size)
    {
        if (_renderer is null) return;
        _renderer.OnResize(size.X, size.Y);

        var logicalSize = _window?.Size;
        if (logicalSize.HasValue && logicalSize.Value.X > 0)
            _contentScale = (float)size.X / logicalSize.Value.X;
        _renderer.ContentScale = _contentScale;
    }

    private void OnKeyDown(IKeyboard kb, Key key, int scancode)
    {
        if (_panel is null) return;

        // F12 closes the window
        if (key == Key.F12)
        {
            Close();
            return;
        }

        if (key == Key.Escape)
        {
            _panel.InputFocused = !_panel.InputFocused;
            return;
        }

        if (!_panel.InputFocused) return;

        switch (key)
        {
            case Key.Enter:
                ExecuteInput();
                break;
            case Key.Backspace:
                if (_panel.InputCursorPosition > 0)
                {
                    _panel.InputText = _panel.InputText.Remove(
                        _panel.InputCursorPosition - 1, 1);
                    _panel.InputCursorPosition--;
                }
                break;
            case Key.Delete:
                if (_panel.InputCursorPosition < _panel.InputText.Length)
                    _panel.InputText = _panel.InputText.Remove(
                        _panel.InputCursorPosition, 1);
                break;
            case Key.Left:
                if (_panel.InputCursorPosition > 0) _panel.InputCursorPosition--;
                break;
            case Key.Right:
                if (_panel.InputCursorPosition < _panel.InputText.Length)
                    _panel.InputCursorPosition++;
                break;
            case Key.Home:
                _panel.InputCursorPosition = 0;
                break;
            case Key.End:
                _panel.InputCursorPosition = _panel.InputText.Length;
                break;
            case Key.Up:
                _panel.NavigateHistory(-1);
                break;
            case Key.Down:
                _panel.NavigateHistory(1);
                break;
            case Key.L:
                // Cmd/Ctrl+L to clear
                if (IsCommandModifier(kb))
                {
                    // Queue clear as a special command
                    var log = _getConsoleLog();
                    log?.Clear();
                }
                break;
        }
    }

    private void OnKeyChar(IKeyboard kb, char c)
    {
        if (_panel is null || !_panel.InputFocused) return;
        if (char.IsControl(c)) return;

        _panel.InputText = _panel.InputText.Insert(
            _panel.InputCursorPosition, c.ToString());
        _panel.InputCursorPosition++;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (_panel is null || _window is null || button != MouseButton.Left) return;

        float x = mouse.Position.X;
        float y = mouse.Position.Y;
        float fbWidth = _window.FramebufferSize.X / _contentScale;

        var hit = _panel.HitTest(x, y, 0, fbWidth);
        switch (hit)
        {
            case DevToolsHitArea.CloseButton:
                Close();
                break;
            case DevToolsHitArea.ClearButton:
                _getConsoleLog()?.Clear();
                break;
            case DevToolsHitArea.InputLine:
                _panel.InputFocused = true;
                _panel.InputCursorPosition = _panel.HitTestInputCursor(x);
                break;
            case DevToolsHitArea.LogArea:
                _panel.InputFocused = false;
                break;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        _panel?.ScrollBy(-wheel.Y * ScrollState.ScrollStep);
    }

    private void ExecuteInput()
    {
        if (_panel is null) return;
        var code = _panel.InputText.Trim();
        if (string.IsNullOrEmpty(code)) return;

        // Log the input command immediately (ConsoleLog is thread-safe)
        var log = _getConsoleLog();
        log?.Add(new ConsoleMessage
        {
            Level = ConsoleMessageLevel.Log,
            Text = "> " + code,
        });

        // Queue execution for the main thread
        ExecutionQueue.Enqueue(code);

        // History and reset
        _panel.AddToHistory(code);
        _panel.InputText = "";
        _panel.InputCursorPosition = 0;
        _panel.ScrollToBottom();
    }

    private static bool IsCommandModifier(IKeyboard kb)
    {
        if (OperatingSystem.IsMacOS())
            return kb.IsKeyPressed(Key.SuperLeft) || kb.IsKeyPressed(Key.SuperRight);
        return kb.IsKeyPressed(Key.ControlLeft) || kb.IsKeyPressed(Key.ControlRight);
    }

    private void DisposeResources()
    {
        if (_inputContext is not null)
        {
            try { _inputContext.Dispose(); } catch { /* may already be disposed */ }
            _inputContext = null;
        }
        _renderer?.Dispose();
        _renderer = null;
        _panel = null;
        _measurer = null;
        if (_window is not null)
        {
            try { _window.Dispose(); } catch { /* window may already be destroyed */ }
            _window = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeResources();
    }
}
