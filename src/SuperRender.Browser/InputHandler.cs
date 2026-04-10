using SuperRender.Browser.Networking;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;
using Silk.NET.Input;

namespace SuperRender.Browser;

/// <summary>
/// Routes keyboard and mouse input to the appropriate browser component.
/// </summary>
public sealed class InputHandler
{
    private readonly BrowserChrome _chrome;
    private readonly TabManager _tabs;
    private readonly ITextMeasurer _measurer;
    private readonly Func<float> _getContentScale;
    private readonly Action<Uri> _navigateCallback;
    private readonly Func<float> _getViewportHeight;
    private bool _isDragging;

    public InputHandler(
        BrowserChrome chrome,
        TabManager tabs,
        ITextMeasurer measurer,
        Func<float> getContentScale,
        Action<Uri> navigateCallback,
        Func<float> getViewportHeight)
    {
        _chrome = chrome;
        _tabs = tabs;
        _measurer = measurer;
        _getContentScale = getContentScale;
        _navigateCallback = navigateCallback;
        _getViewportHeight = getViewportHeight;
    }

    public void OnMouseDown(float physicalX, float physicalY, float viewportWidth)
    {
        float scale = _getContentScale();
        float x = physicalX / scale;
        float y = physicalY / scale;
        float logicalWidth = viewportWidth / scale;

        if (y < BrowserChrome.TotalChromeHeight)
        {
            var hit = BrowserChrome.HitTest(x, y, logicalWidth, _tabs);
            _chrome.AddressBarFocused = false;

            switch (hit.Area)
            {
                case ChromeHitArea.TabClicked:
                    _tabs.SwitchTab(hit.TabIndex);
                    UpdateAddressFromTab();
                    break;

                case ChromeHitArea.NewTabButton:
                    _tabs.CreateTab();
                    _chrome.AddressText = "";
                    _chrome.CursorPosition = 0;
                    _chrome.AddressBarFocused = true;
                    break;

                case ChromeHitArea.CloseTabButton:
                    _tabs.CloseTab(hit.TabIndex);
                    UpdateAddressFromTab();
                    break;

                case ChromeHitArea.AddressBar:
                    _chrome.AddressBarFocused = true;
                    _chrome.CursorPosition = HitTestAddressBarCursor(x);
                    break;

                case ChromeHitArea.GoButton:
                    TriggerNavigation();
                    break;

                case ChromeHitArea.ReloadButton:
                    if (_tabs.ActiveTab?.CurrentUri is not null)
                        _navigateCallback(_tabs.ActiveTab.CurrentUri);
                    break;

                case ChromeHitArea.BackButton:
                    NavigateHistory(forward: false);
                    break;

                case ChromeHitArea.ForwardButton:
                    NavigateHistory(forward: true);
                    break;
            }
        }
        else
        {
            // Click in content area - unfocus address bar, start text selection
            _chrome.AddressBarFocused = false;

            var tab = _tabs.ActiveTab;
            if (tab?.LayoutRoot is not null)
            {
                float contentX = x;
                float contentY = y - BrowserChrome.TotalChromeHeight + tab.ScrollOffsetY;

                // Check for link clicks
                var hitBox = HitTestLayoutBox(tab.LayoutRoot, contentX, contentY);
                if (hitBox is not null)
                {
                    var anchor = FindAnchorElement(hitBox.DomNode);
                    if (anchor is not null)
                    {
                        var href = anchor.GetAttribute("href");
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            var targetUri = UrlResolver.Resolve(href, tab.CurrentUri);
                            var target = anchor.GetAttribute("target");
                            if (target is not null && target.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                            {
                                var newTab = _tabs.CreateTab();
                                _navigateCallback(targetUri);
                            }
                            else
                            {
                                _navigateCallback(targetUri);
                            }
                            return;
                        }
                    }
                }

                // Text selection
                var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
                var textHit = TextHitTester.HitTest(allRuns, contentX, contentY, _measurer);
                if (textHit.HasValue)
                {
                    tab.Selection.Start = new TextPosition(textHit.Value.runIndex, textHit.Value.charOffset);
                    tab.Selection.End = tab.Selection.Start;
                    _isDragging = true;
                }
                else
                {
                    tab.Selection.Clear();
                }
            }
        }
    }

    public void OnKeyDown(Key key, bool ctrl = false, bool shift = false)
    {
        // Keyboard shortcuts (processed before address bar input)
        if (ctrl)
        {
            switch (key)
            {
                case Key.T:
                    _tabs.CreateTab();
                    _chrome.AddressText = "";
                    _chrome.CursorPosition = 0;
                    _chrome.AddressBarFocused = true;
                    return;

                case Key.W:
                    _tabs.CloseTab(_tabs.ActiveTabIndex);
                    UpdateAddressFromTab();
                    return;

                case Key.Tab:
                    if (shift)
                    {
                        int prev = _tabs.ActiveTabIndex - 1;
                        if (prev < 0) prev = _tabs.Tabs.Count - 1;
                        _tabs.SwitchTab(prev);
                    }
                    else
                    {
                        int next = (_tabs.ActiveTabIndex + 1) % _tabs.Tabs.Count;
                        _tabs.SwitchTab(next);
                    }
                    UpdateAddressFromTab();
                    return;

                case Key.L:
                    _chrome.AddressBarFocused = true;
                    _chrome.CursorPosition = _chrome.AddressText.Length;
                    return;

                case Key.R:
                    if (_tabs.ActiveTab?.CurrentUri is not null)
                        _navigateCallback(_tabs.ActiveTab.CurrentUri);
                    return;
            }
        }

        if (key == Key.F5)
        {
            if (_tabs.ActiveTab?.CurrentUri is not null)
                _navigateCallback(_tabs.ActiveTab.CurrentUri);
            return;
        }

        if (key == Key.Escape)
        {
            if (_chrome.AddressBarFocused)
            {
                _chrome.AddressBarFocused = false;
                UpdateAddressFromTab();
            }
            return;
        }

        // Content scrolling (when address bar is not focused)
        if (!_chrome.AddressBarFocused)
        {
            var tab = _tabs.ActiveTab;
            if (tab is not null)
            {
                float contentAreaHeight = _getViewportHeight() - BrowserChrome.TotalChromeHeight;
                if (contentAreaHeight < 0) contentAreaHeight = 0;
                const float scrollStep = 40f;

                switch (key)
                {
                    case Key.Up:
                        tab.ScrollOffsetY -= scrollStep;
                        ClampScrollOffset(tab);
                        return;
                    case Key.Down:
                        tab.ScrollOffsetY += scrollStep;
                        ClampScrollOffset(tab);
                        return;
                    case Key.PageUp:
                        tab.ScrollOffsetY -= contentAreaHeight;
                        ClampScrollOffset(tab);
                        return;
                    case Key.PageDown:
                    case Key.Space:
                        tab.ScrollOffsetY += contentAreaHeight;
                        ClampScrollOffset(tab);
                        return;
                    case Key.Home:
                        tab.ScrollOffsetY = 0;
                        return;
                    case Key.End:
                        tab.ScrollOffsetY = Math.Max(0, tab.ContentHeight - contentAreaHeight);
                        return;
                }
            }
            return;
        }

        // Address bar key handling
        switch (key)
        {
            case Key.Enter:
                TriggerNavigation();
                _chrome.AddressBarFocused = false;
                break;

            case Key.Backspace:
                if (_chrome.CursorPosition > 0 && _chrome.AddressText.Length > 0)
                {
                    _chrome.AddressText = _chrome.AddressText.Remove(_chrome.CursorPosition - 1, 1);
                    _chrome.CursorPosition--;
                }
                break;

            case Key.Delete:
                if (_chrome.CursorPosition < _chrome.AddressText.Length)
                {
                    _chrome.AddressText = _chrome.AddressText.Remove(_chrome.CursorPosition, 1);
                }
                break;

            case Key.Left:
                if (_chrome.CursorPosition > 0) _chrome.CursorPosition--;
                break;

            case Key.Right:
                if (_chrome.CursorPosition < _chrome.AddressText.Length) _chrome.CursorPosition++;
                break;

            case Key.Home:
                _chrome.CursorPosition = 0;
                break;

            case Key.End:
                _chrome.CursorPosition = _chrome.AddressText.Length;
                break;
        }
    }

    public void OnCharInput(char c)
    {
        if (!_chrome.AddressBarFocused) return;
        if (char.IsControl(c)) return;

        _chrome.AddressText = _chrome.AddressText.Insert(_chrome.CursorPosition, c.ToString());
        _chrome.CursorPosition++;
    }

    private void TriggerNavigation()
    {
        if (string.IsNullOrWhiteSpace(_chrome.AddressText)) return;
        var uri = UrlResolver.NormalizeAddress(_chrome.AddressText);
        _navigateCallback(uri);
    }

    private void UpdateAddressFromTab()
    {
        var tab = _tabs.ActiveTab;
        _chrome.AddressText = tab?.CurrentUri?.ToString() ?? "";
        _chrome.CursorPosition = _chrome.AddressText.Length;
    }

    private void NavigateHistory(bool forward)
    {
        var tab = _tabs.ActiveTab;
        if (tab is null) return;

        var uri = forward ? tab.GoForward() : tab.GoBack();
        if (uri is not null)
        {
            _chrome.AddressText = uri.ToString();
            _chrome.CursorPosition = _chrome.AddressText.Length;
            // Navigate with history flag so we don't push a new entry
            _ = tab.NavigateAsync(uri, isHistoryNavigation: true);
        }
    }

    private void ClampScrollOffset(Tab tab)
    {
        float contentAreaHeight = _getViewportHeight() - BrowserChrome.TotalChromeHeight;
        if (contentAreaHeight < 0) contentAreaHeight = 0;
        float maxScroll = Math.Max(0, tab.ContentHeight - contentAreaHeight);
        tab.ScrollOffsetY = Math.Clamp(tab.ScrollOffsetY, 0, maxScroll);
    }

    public ContextMenu? OnRightClick(float physicalX, float physicalY, float viewportWidth)
    {
        float scale = _getContentScale();
        float x = physicalX / scale;
        float y = physicalY / scale;
        float logicalWidth = viewportWidth / scale;

        if (y < BrowserChrome.TotalChromeHeight)
        {
            var hit = BrowserChrome.HitTest(x, y, logicalWidth, _tabs);
            if (hit.Area == ChromeHitArea.AddressBar)
                return BuildAddressBarContextMenu(x, y);
            return null;
        }

        return BuildContentContextMenu(x, y);
    }

    public void OnMouseUp(float physicalX, float physicalY, float viewportWidth)
    {
        _isDragging = false;
    }

    public void OnMouseMove(float physicalX, float physicalY, float viewportWidth)
    {
        if (!_isDragging) return;

        float scale = _getContentScale();
        float x = physicalX / scale;
        float y = physicalY / scale;

        var tab = _tabs.ActiveTab;
        if (tab?.LayoutRoot is null) return;

        float contentX = x;
        float contentY = y - BrowserChrome.TotalChromeHeight + tab.ScrollOffsetY;
        var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
        var hit = TextHitTester.HitTest(allRuns, contentX, contentY, _measurer);
        if (hit.HasValue)
        {
            tab.Selection.End = new TextPosition(hit.Value.runIndex, hit.Value.charOffset);
        }
    }

    private ContextMenu BuildAddressBarContextMenu(float x, float y)
    {
        var items = new List<ContextMenuItem>();
        bool hasText = _chrome.AddressText.Length > 0;

        items.Add(new ContextMenuItem
        {
            Label = "Cut",
            Enabled = hasText,
            Action = () =>
            {
                if (_chrome.AddressText.Length > 0)
                {
                    ClipboardHelper.SetText(_chrome.AddressText);
                    _chrome.AddressText = "";
                    _chrome.CursorPosition = 0;
                }
            },
        });

        items.Add(new ContextMenuItem
        {
            Label = "Copy",
            Enabled = hasText,
            Action = () =>
            {
                if (_chrome.AddressText.Length > 0)
                    ClipboardHelper.SetText(_chrome.AddressText);
            },
        });

        items.Add(new ContextMenuItem
        {
            Label = "Paste",
            Enabled = true,
            Action = () =>
            {
                var clipboard = ClipboardHelper.GetText();
                if (!string.IsNullOrEmpty(clipboard))
                {
                    _chrome.AddressText = _chrome.AddressText.Insert(
                        _chrome.CursorPosition, clipboard);
                    _chrome.CursorPosition += clipboard.Length;
                }
            },
        });

        items.Add(new ContextMenuItem
        {
            Label = "",
            Action = () => { },
            IsSeparator = true,
        });

        items.Add(new ContextMenuItem
        {
            Label = "Select All",
            Enabled = hasText,
            Action = () =>
            {
                _chrome.AddressBarFocused = true;
                _chrome.CursorPosition = _chrome.AddressText.Length;
            },
        });

        return new ContextMenu(x, y, items);
    }

    private int HitTestAddressBarCursor(float clickX)
    {
        const float addressFontSize = 13f;
        float btnX = BrowserChrome.AddressBarPadding
                   + BrowserChrome.ButtonWidth + 4   // Back
                   + BrowserChrome.ButtonWidth + 4   // Forward
                   + BrowserChrome.ButtonWidth + 8;  // Reload
        float textStartX = btnX + 6;
        float relX = clickX - textStartX;
        if (relX <= 0) return 0;

        string text = _chrome.AddressText;
        for (int i = 1; i <= text.Length; i++)
        {
            float w = _measurer.MeasureWidth(text[..i], addressFontSize);
            if (w > relX)
            {
                float prevW = i > 1 ? _measurer.MeasureWidth(text[..(i - 1)], addressFontSize) : 0;
                return (relX - prevW < w - relX) ? i - 1 : i;
            }
        }
        return text.Length;
    }

    private ContextMenu BuildContentContextMenu(float x, float y)
    {
        var tab = _tabs.ActiveTab;
        var items = new List<ContextMenuItem>();

        bool hasSelection = tab?.Selection.HasSelection == true;

        items.Add(new ContextMenuItem
        {
            Label = "Copy",
            Enabled = hasSelection,
            Action = () =>
            {
                if (tab is not null)
                {
                    var selectedText = GetSelectedText(tab);
                    if (selectedText.Length > 0)
                        ClipboardHelper.SetText(selectedText);
                }
            },
        });

        items.Add(new ContextMenuItem
        {
            Label = "Select All",
            Enabled = tab?.LayoutRoot is not null,
            Action = () => SelectAllText(tab),
        });

        items.Add(new ContextMenuItem
        {
            Label = "",
            Action = () => { },
            IsSeparator = true,
        });

        items.Add(new ContextMenuItem
        {
            Label = "View Source",
            Enabled = tab?.Document is not null,
            Action = () => ViewSource(tab),
        });

        return new ContextMenu(x, y, items);
    }

    private static string GetSelectedText(Tab tab)
    {
        if (!tab.Selection.HasSelection || tab.LayoutRoot is null)
            return "";

        var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
        var (start, end) = tab.Selection.GetOrdered();

        var sb = new System.Text.StringBuilder();
        for (int i = start.RunIndex; i <= end.RunIndex && i < allRuns.Count; i++)
        {
            var run = allRuns[i];
            int startChar = (i == start.RunIndex) ? start.CharOffset : 0;
            int endChar = (i == end.RunIndex) ? end.CharOffset : run.Text.Length;
            if (startChar < endChar && startChar < run.Text.Length)
            {
                endChar = Math.Min(endChar, run.Text.Length);
                sb.Append(run.Text[startChar..endChar]);
            }
        }

        return sb.ToString();
    }

    private static void SelectAllText(Tab? tab)
    {
        if (tab?.LayoutRoot is null) return;

        var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
        if (allRuns.Count == 0) return;

        tab.Selection.Start = new TextPosition(0, 0);
        tab.Selection.End = new TextPosition(allRuns.Count - 1, allRuns[^1].Text.Length);
    }

    private void ViewSource(Tab? tab)
    {
        if (tab?.Document is null) return;

        // Serialize the document's HTML (simple approach using source reconstruction)
        var html = GetDocumentHtml(tab.Document);
        var sourceHtml = $"<html><head><style>body {{ margin: 16px; font-size: 13px; }} pre {{ white-space: pre-wrap; }}</style></head><body><pre>{EscapeHtml(html)}</pre></body></html>";

        var newTab = _tabs.CreateTab();

        // Load source HTML via reflection (same approach as BrowserWindow.LoadWelcomePage)
        var pipelineField = typeof(Tab).GetField("_pipeline",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var measurerField = typeof(Tab).GetField("_measurer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (pipelineField is not null && measurerField is not null)
        {
            var m = (ITextMeasurer)measurerField.GetValue(newTab)!;
            var rp = new SuperRender.Core.RenderPipeline(m, useUserAgentStylesheet: true);
            rp.LoadHtml(sourceHtml);
            pipelineField.SetValue(newTab, rp);
        }

        _chrome.AddressText = $"view-source:{tab.CurrentUri}";
        _chrome.CursorPosition = _chrome.AddressText.Length;
    }

    private static string GetDocumentHtml(SuperRender.Core.Dom.Document doc)
    {
        var sb = new System.Text.StringBuilder();
        SerializeNode(doc, sb);
        return sb.ToString();
    }

    private static void SerializeNode(SuperRender.Core.Dom.Node node, System.Text.StringBuilder sb)
    {
        if (node is SuperRender.Core.Dom.TextNode text)
        {
            sb.Append(text.Data);
            return;
        }

        if (node is Element el)
        {
            sb.Append('<').Append(el.TagName);
            foreach (var attr in el.Attributes)
                sb.Append(' ').Append(attr.Key).Append("=\"").Append(attr.Value).Append('"');
            sb.Append('>');

            foreach (var child in el.Children)
                SerializeNode(child, sb);

            sb.Append("</").Append(el.TagName).Append('>');
            return;
        }

        // Document or other node types
        foreach (var child in node.Children)
            SerializeNode(child, sb);
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static Element? FindAnchorElement(Node? node)
    {
        while (node != null)
        {
            if (node is Element el && el.TagName.Equals("a", StringComparison.OrdinalIgnoreCase))
                return el;
            node = node.Parent;
        }
        return null;
    }

    private static LayoutBox? HitTestLayoutBox(LayoutBox box, float x, float y)
    {
        // Check children first (front-to-back, reverse order)
        for (int i = box.Children.Count - 1; i >= 0; i--)
        {
            var hit = HitTestLayoutBox(box.Children[i], x, y);
            if (hit != null) return hit;
        }
        // Check this box
        var rect = box.Dimensions.BorderRect;
        if (x >= rect.X && x < rect.Right && y >= rect.Y && y < rect.Bottom)
            return box;
        return null;
    }
}
