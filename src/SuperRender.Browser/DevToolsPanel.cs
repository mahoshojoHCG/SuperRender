using SuperRender.Core;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;

namespace SuperRender.Browser;

/// <summary>
/// Hit-test areas within the developer tools panel.
/// </summary>
public enum DevToolsHitArea
{
    None,
    ResizeHandle,
    Toolbar,
    CloseButton,
    ClearButton,
    ConsoleTab,
    LogArea,
    InputLine,
}

/// <summary>
/// Chrome-style developer tools console panel.
/// Provides a Console tab for executing JS and viewing console output.
/// Rendered either in a standalone window or embedded.
/// </summary>
public sealed class DevToolsPanel
{
    // Layout constants
    public const float DefaultHeight = 250f;
    public const float MinHeight = 120f;
    public const float MaxHeightFraction = 0.6f;
    public const float ToolbarHeight = 28f;
    public const float InputLineHeight = 24f;
    public const float FontSize = 12f;
    public const float LinePadding = 2f;
    public const float LeftPadding = 8f;

    // Colors (light theme matching browser chrome)
    private static readonly Color PanelBg = Color.FromRgb(248, 249, 250);
    private static readonly Color ToolbarBg = Color.FromRgb(237, 239, 241);
    private static readonly Color BorderColor = Color.FromRgb(173, 181, 189);
    private static readonly Color InputBg = Color.White;
    private static readonly Color TextDefault = Color.FromRgb(33, 37, 41);
    private static readonly Color TextWarn = Color.FromRgb(181, 137, 0);
    private static readonly Color TextError = Color.FromRgb(220, 53, 69);
    private static readonly Color TextResult = Color.FromRgb(0, 128, 0);
    private static readonly Color TextDebug = Color.FromRgb(108, 117, 125);
    private static readonly Color PromptColor = Color.FromRgb(0, 123, 255);
    private static readonly Color ActiveTabBg = Color.White;
    private static readonly Color ActiveTabBorder = Color.FromRgb(0, 123, 255);
    private static readonly Color ButtonText = Color.FromRgb(73, 80, 87);
    private static readonly Color ButtonBg = Color.FromRgb(233, 236, 239);
    private static readonly Color WarnBg = Color.FromRgb(255, 249, 230);
    private static readonly Color ErrorBg = Color.FromRgb(255, 240, 240);

    private readonly ITextMeasurer _measurer;

    // State
    public float Height { get; set; } = DefaultHeight;
    public string InputText { get; set; } = "";
    public int InputCursorPosition { get; set; }
    public bool InputFocused { get; set; }
    private float _logScrollY;
    private int _lastMessageCount;
    private bool _autoScroll = true;

    // Input history
    private readonly List<string> _inputHistory = new();
    private int _historyIndex = -1;
    private string _savedInput = "";

    public DevToolsPanel(ITextMeasurer measurer)
    {
        _measurer = measurer;
    }

    /// <summary>
    /// The total height the panel occupies.
    /// </summary>
    public float TotalPanelHeight => Height;

    private static float LineHeight => FontSize + LinePadding * 2;

    private float LogAreaHeight => Height - ToolbarHeight - 1 - InputLineHeight;

    /// <summary>
    /// Builds the paint list for the developer tools panel.
    /// </summary>
    public PaintList BuildPaintList(float viewportWidth, float panelTopY, ConsoleLog log)
    {
        var list = new PaintList();
        float y = panelTopY;

        // Toolbar background
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, y, viewportWidth, ToolbarHeight),
            Color = ToolbarBg,
        });

        // Console tab (active)
        float tabX = 8;
        float tabW = _measurer.MeasureWidth("Console", FontSize) + 16;
        list.Add(new FillRectCommand
        {
            Rect = new RectF(tabX, y, tabW, ToolbarHeight),
            Color = ActiveTabBg,
        });
        // Active tab bottom indicator
        list.Add(new FillRectCommand
        {
            Rect = new RectF(tabX, y + ToolbarHeight - 2, tabW, 2),
            Color = ActiveTabBorder,
        });
        list.Add(new DrawTextCommand
        {
            Text = "Console",
            X = tabX + 8,
            Y = y + (ToolbarHeight - FontSize) / 2,
            FontSize = FontSize,
            Color = TextDefault,
        });

        // Clear button
        float clearW = _measurer.MeasureWidth("Clear", FontSize) + 12;
        float clearX = viewportWidth - 8 - 24 - 8 - clearW;
        float btnY = y + 4;
        float btnH = ToolbarHeight - 8;
        list.Add(new FillRectCommand
        {
            Rect = new RectF(clearX, btnY, clearW, btnH),
            Color = ButtonBg,
        });
        list.Add(new DrawTextCommand
        {
            Text = "Clear",
            X = clearX + 6,
            Y = y + (ToolbarHeight - FontSize) / 2,
            FontSize = FontSize,
            Color = ButtonText,
        });

        // Close button "X"
        float closeX = viewportWidth - 8 - 20;
        list.Add(new DrawTextCommand
        {
            Text = "X",
            X = closeX,
            Y = y + (ToolbarHeight - FontSize) / 2,
            FontSize = FontSize,
            Color = ButtonText,
        });

        // Toolbar bottom border
        y += ToolbarHeight;
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, y, viewportWidth, 1),
            Color = BorderColor,
        });
        y += 1;

        // Log area
        float logAreaTop = y;
        float logAreaH = LogAreaHeight;
        if (logAreaH < 0) logAreaH = 0;

        // Log area background
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, logAreaTop, viewportWidth, logAreaH),
            Color = PanelBg,
        });

        // Render messages
        var messages = log.GetSnapshot();

        // Auto-scroll when new messages arrive
        if (messages.Count > _lastMessageCount && _autoScroll)
        {
            float totalContentH = messages.Count * LineHeight;
            if (totalContentH > logAreaH)
                _logScrollY = totalContentH - logAreaH;
        }
        _lastMessageCount = messages.Count;

        float lineHeight = LineHeight;
        for (int i = 0; i < messages.Count; i++)
        {
            float msgY = logAreaTop + (i * lineHeight) - _logScrollY;

            // Skip messages outside visible area
            if (msgY + lineHeight < logAreaTop) continue;
            if (msgY > logAreaTop + logAreaH) break;

            var msg = messages[i];

            // Row background for warn/error
            if (msg.Level == ConsoleMessageLevel.Warn)
            {
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(0, msgY, viewportWidth, lineHeight),
                    Color = WarnBg,
                });
            }
            else if (msg.Level == ConsoleMessageLevel.Error)
            {
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(0, msgY, viewportWidth, lineHeight),
                    Color = ErrorBg,
                });
            }

            // Separator line between messages
            list.Add(new FillRectCommand
            {
                Rect = new RectF(0, msgY + lineHeight - 1, viewportWidth, 1),
                Color = Color.FromRgba(0, 0, 0, 10),
            });

            // Level prefix icon
            var color = GetMessageColor(msg.Level);
            string prefix = GetLevelPrefix(msg.Level);
            float textX = LeftPadding;

            if (prefix.Length > 0)
            {
                list.Add(new DrawTextCommand
                {
                    Text = prefix,
                    X = textX,
                    Y = msgY + LinePadding,
                    FontSize = FontSize,
                    Color = color,
                });
                textX += _measurer.MeasureWidth(prefix + " ", FontSize);
            }

            // Message text
            list.Add(new DrawTextCommand
            {
                Text = msg.Text,
                X = textX,
                Y = msgY + LinePadding,
                FontSize = FontSize,
                Color = color,
            });
        }

        // Toolbar re-draw on top to occlude scrolled messages
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, panelTopY, viewportWidth, ToolbarHeight + 1),
            Color = Color.Transparent,
        });

        // Input line
        float inputY = logAreaTop + logAreaH;

        // Input line top border
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, inputY, viewportWidth, 1),
            Color = BorderColor,
        });

        // Input line background
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, inputY + 1, viewportWidth, InputLineHeight - 1),
            Color = InputBg,
        });

        // Prompt ">"
        list.Add(new DrawTextCommand
        {
            Text = ">",
            X = LeftPadding,
            Y = inputY + 1 + (InputLineHeight - 1 - FontSize) / 2,
            FontSize = FontSize,
            Color = PromptColor,
        });

        // Input text
        float inputTextX = LeftPadding + _measurer.MeasureWidth("> ", FontSize);
        float inputTextY = inputY + 1 + (InputLineHeight - 1 - FontSize) / 2;
        list.Add(new DrawTextCommand
        {
            Text = InputText,
            X = inputTextX,
            Y = inputTextY,
            FontSize = FontSize,
            Color = TextDefault,
        });

        // Cursor
        if (InputFocused)
        {
            string beforeCursor = InputText[..Math.Min(InputCursorPosition, InputText.Length)];
            float cursorX = inputTextX + _measurer.MeasureWidth(beforeCursor, FontSize);
            list.Add(new FillRectCommand
            {
                Rect = new RectF(cursorX, inputY + 4, 1.5f, InputLineHeight - 7),
                Color = TextDefault,
            });
        }

        return list;
    }

    /// <summary>
    /// Hit-tests a coordinate within the DevTools panel.
    /// </summary>
    public DevToolsHitArea HitTest(float x, float y, float panelTopY, float viewportWidth)
    {
        if (y < panelTopY) return DevToolsHitArea.None;

        float localY = y - panelTopY;

        // Toolbar
        if (localY < ToolbarHeight)
        {
            // Close button
            float closeX = viewportWidth - 8 - 20;
            if (x >= closeX)
                return DevToolsHitArea.CloseButton;

            // Clear button
            float clearW = _measurer.MeasureWidth("Clear", FontSize) + 12;
            float clearX = viewportWidth - 8 - 24 - 8 - clearW;
            if (x >= clearX && x <= clearX + clearW)
                return DevToolsHitArea.ClearButton;

            return DevToolsHitArea.ConsoleTab;
        }

        // Input line (at bottom)
        float inputLineTop = panelTopY + Height - InputLineHeight;
        if (y >= inputLineTop)
            return DevToolsHitArea.InputLine;

        // Log area
        return DevToolsHitArea.LogArea;
    }

    /// <summary>
    /// Scrolls the log area by the given delta.
    /// </summary>
    public void ScrollBy(float delta)
    {
        float maxScroll = GetMaxLogScroll();
        _logScrollY = Math.Clamp(_logScrollY + delta, 0, maxScroll);

        // Disable auto-scroll if user scrolled up
        _autoScroll = _logScrollY >= maxScroll - 1;
    }

    /// <summary>
    /// Scrolls the log to the bottom.
    /// </summary>
    public void ScrollToBottom()
    {
        _logScrollY = GetMaxLogScroll();
        _autoScroll = true;
    }

    /// <summary>
    /// Adds a command to the input history and resets the history cursor.
    /// </summary>
    public void AddToHistory(string command)
    {
        _inputHistory.Add(command);
        _historyIndex = -1;
        _savedInput = "";
    }

    /// <summary>
    /// Navigates input history. direction=-1 for older, +1 for newer.
    /// </summary>
    public void NavigateHistory(int direction)
    {
        if (_inputHistory.Count == 0) return;

        if (_historyIndex == -1 && direction == -1)
        {
            // Save current input and start browsing history
            _savedInput = InputText;
            _historyIndex = _inputHistory.Count - 1;
        }
        else if (_historyIndex >= 0)
        {
            _historyIndex += direction;
        }

        if (_historyIndex < 0)
        {
            // Returned past the newest entry
            _historyIndex = -1;
            InputText = _savedInput;
            InputCursorPosition = InputText.Length;
            return;
        }

        if (_historyIndex >= _inputHistory.Count)
        {
            _historyIndex = _inputHistory.Count - 1;
            return;
        }

        InputText = _inputHistory[_historyIndex];
        InputCursorPosition = InputText.Length;
    }

    /// <summary>
    /// Hit-tests the input line to find cursor position.
    /// </summary>
    public int HitTestInputCursor(float clickX)
    {
        float promptW = _measurer.MeasureWidth("> ", FontSize);
        float inputTextX = LeftPadding + promptW;
        float relX = clickX - inputTextX;
        if (relX <= 0) return 0;

        for (int i = 1; i <= InputText.Length; i++)
        {
            float w = _measurer.MeasureWidth(InputText[..i], FontSize);
            if (w > relX)
            {
                float prevW = i > 1 ? _measurer.MeasureWidth(InputText[..(i - 1)], FontSize) : 0;
                return (relX - prevW < w - relX) ? i - 1 : i;
            }
        }
        return InputText.Length;
    }

    private float GetMaxLogScroll()
    {
        float totalContentH = _lastMessageCount * LineHeight;
        float logH = LogAreaHeight;
        return Math.Max(0, totalContentH - logH);
    }

    private static Color GetMessageColor(ConsoleMessageLevel level) => level switch
    {
        ConsoleMessageLevel.Warn => TextWarn,
        ConsoleMessageLevel.Error => TextError,
        ConsoleMessageLevel.Debug => TextDebug,
        ConsoleMessageLevel.Info => TextResult,
        _ => TextDefault,
    };

    private static string GetLevelPrefix(ConsoleMessageLevel level) => level switch
    {
        ConsoleMessageLevel.Warn => "!",
        ConsoleMessageLevel.Error => "x",
        _ => "",
    };
}
