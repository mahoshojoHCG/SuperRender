using SuperRender.Browser.Networking;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Painting;
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
    private readonly Action _goBackCallback;
    private readonly Action _goForwardCallback;
    private readonly Action _onToggleDevTools;
    private bool _isDragging;
    private bool _isAddressBarDragging;
    private float _mouseDownX;
    private float _mouseDownY;
    private Node? _mouseDownNode;
    private const float ClickThreshold = 5f;
    private Node? _currentHoverTarget;
    private Node? _currentActiveNode;
    private Element? _currentFocusedElement;

    public InputHandler(
        BrowserChrome chrome,
        TabManager tabs,
        ITextMeasurer measurer,
        Func<float> getContentScale,
        Action<Uri> navigateCallback,
        Action goBackCallback,
        Action goForwardCallback,
        Action onToggleDevTools)
    {
        _chrome = chrome;
        _tabs = tabs;
        _measurer = measurer;
        _getContentScale = getContentScale;
        _navigateCallback = navigateCallback;
        _goBackCallback = goBackCallback;
        _goForwardCallback = goForwardCallback;
        _onToggleDevTools = onToggleDevTools;
    }

    public void OnScroll(float deltaY)
    {
        var tab = _tabs.ActiveTab;
        if (tab is null) return;
        tab.Scroll.ScrollBy(-deltaY * ScrollState.ScrollStep);
    }

    public void OnMouseDown(float physicalX, float physicalY, float viewportWidth)
    {
        float scale = _getContentScale();
        float x = physicalX / scale;
        float y = physicalY / scale;
        float logicalWidth = viewportWidth / scale;

        _mouseDownX = x;
        _mouseDownY = y;

        if (y < BrowserChrome.TotalChromeHeight)
            HandleChromeMouseDown(x, y, logicalWidth);
        else
            HandleContentMouseDown(x, y);
    }

    private void HandleChromeMouseDown(float x, float y, float logicalWidth)
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
                int cursorPos = HitTestAddressBarCursor(x);
                _chrome.CursorPosition = cursorPos;
                _chrome.SelectionStart = cursorPos;
                _chrome.SelectionEnd = cursorPos;
                _isAddressBarDragging = true;
                break;

            case ChromeHitArea.GoButton:
                TriggerNavigation();
                break;

            case ChromeHitArea.ReloadButton:
                if (_tabs.ActiveTab?.CurrentUri is not null)
                    _navigateCallback(_tabs.ActiveTab.CurrentUri);
                break;

            case ChromeHitArea.BackButton:
                _goBackCallback();
                break;

            case ChromeHitArea.ForwardButton:
                _goForwardCallback();
                break;
        }
    }

    private void HandleContentMouseDown(float x, float y)
    {
        // Click in content area - unfocus address bar, start text selection
        _chrome.AddressBarFocused = false;
        _mouseDownNode = null;

        var tab = _tabs.ActiveTab;
        if (tab?.LayoutRoot is not null)
        {
            float contentX = x;
            float contentY = y - BrowserChrome.TotalChromeHeight + tab.Scroll.ScrollY;

            // Hit-test layout boxes for DOM event dispatch
            var hitBox = LayoutBoxHitTester.HitTest(tab.LayoutRoot, contentX, contentY);
            if (hitBox?.DomNode is not null)
            {
                _mouseDownNode = hitBox.DomNode;

                // Set :active state on the target element
                _currentActiveNode = hitBox.DomNode;
                InteractionStateHelper.SetActive(hitBox.DomNode);

                // Set :focus on the nearest focusable element, clear previous focus
                var targetElement = hitBox.DomNode as Element
                    ?? hitBox.DomNode.Parent as Element;
                _currentFocusedElement = InteractionStateHelper.SetFocus(
                    targetElement, _currentFocusedElement);

                if (tab.Document is not null)
                    tab.Document.NeedsLayout = true;

                hitBox.DomNode.DispatchEvent(new MouseEvent
                {
                    Type = "mousedown", Bubbles = true, Cancelable = true,
                    ClientX = contentX, ClientY = contentY, Button = 0,
                });
            }

            var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
            var hit = TextHitTester.HitTest(allRuns, contentX, contentY, _measurer);
            if (hit.HasValue)
            {
                tab.Selection.Start = new TextPosition(hit.Value.runIndex, hit.Value.charOffset);
                tab.Selection.End = tab.Selection.Start;
                _isDragging = true;
            }
            else
            {
                tab.Selection.Clear();
            }
        }
    }

    public void OnKeyDown(Key key, IKeyboard kb)
    {
        bool cmd = IsCommandModifier(kb);
        bool shift = IsShiftPressed(kb);

        if (HandleGlobalShortcut(key, cmd, shift))
            return;

        // Address bar key handling
        if (_chrome.AddressBarFocused)
        {
            HandleAddressBarKeyDown(key);
            return;
        }

        // Content area key handling (scrolling)
        HandleContentKeyDown(key);
    }

    private bool HandleGlobalShortcut(Key key, bool cmd, bool shift)
    {
        // Global shortcuts (always active, regardless of focus)
        if (cmd)
        {
            // Cmd+Shift+I toggles DevTools
            if (shift && key == Key.I)
            {
                _onToggleDevTools();
                return true;
            }

            switch (key)
            {
                case Key.T:
                    _tabs.CreateTab();
                    _chrome.AddressText = "";
                    _chrome.CursorPosition = 0;
                    _chrome.AddressBarFocused = true;
                    return true;
                case Key.W:
                    _tabs.CloseTab(_tabs.ActiveTabIndex);
                    UpdateAddressFromTab();
                    return true;
                case Key.Tab:
                    if (_tabs.Tabs.Count > 1)
                    {
                        int nextIdx = shift
                            ? (_tabs.ActiveTabIndex - 1 + _tabs.Tabs.Count) % _tabs.Tabs.Count
                            : (_tabs.ActiveTabIndex + 1) % _tabs.Tabs.Count;
                        _tabs.SwitchTab(nextIdx);
                        UpdateAddressFromTab();
                    }
                    return true;
                case Key.L:
                    _chrome.AddressBarFocused = true;
                    _chrome.SelectionStart = 0;
                    _chrome.SelectionEnd = _chrome.AddressText.Length;
                    _chrome.CursorPosition = _chrome.AddressText.Length;
                    return true;
                case Key.R:
                    ReloadActiveTab();
                    return true;
                case Key.LeftBracket:
                case Key.Left:
                    _goBackCallback();
                    return true;
                case Key.RightBracket:
                case Key.Right:
                    _goForwardCallback();
                    return true;
            }
        }

        // F12 toggles DevTools
        if (key == Key.F12)
        {
            _onToggleDevTools();
            return true;
        }

        if (key == Key.F5)
        {
            ReloadActiveTab();
            return true;
        }

        if (key == Key.Escape)
        {
            if (_chrome.AddressBarFocused)
            {
                _chrome.AddressBarFocused = false;
                UpdateAddressFromTab();
            }
            return true;
        }

        return false;
    }

    private void HandleAddressBarKeyDown(Key key)
    {
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
                    _chrome.AddressText = _chrome.AddressText.Remove(_chrome.CursorPosition, 1);
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

    private void HandleContentKeyDown(Key key)
    {
        var scroll = _tabs.ActiveTab?.Scroll;
        if (scroll is null) return;

        switch (key)
        {
            case Key.Up:
                scroll.ScrollBy(-ScrollState.ScrollStep);
                break;
            case Key.Down:
                scroll.ScrollBy(ScrollState.ScrollStep);
                break;
            case Key.PageUp:
                scroll.PageUp();
                break;
            case Key.PageDown:
            case Key.Space:
                scroll.PageDown();
                break;
            case Key.Home:
                scroll.ScrollToTop();
                break;
            case Key.End:
                scroll.ScrollToBottom();
                break;
        }
    }

    private void ReloadActiveTab()
    {
        if (_tabs.ActiveTab?.CurrentUri is not null)
            _navigateCallback(_tabs.ActiveTab.CurrentUri);
    }

    private static bool IsCommandModifier(IKeyboard kb)
    {
        if (OperatingSystem.IsMacOS())
            return kb.IsKeyPressed(Key.SuperLeft) || kb.IsKeyPressed(Key.SuperRight);
        return kb.IsKeyPressed(Key.ControlLeft) || kb.IsKeyPressed(Key.ControlRight);
    }

    private static bool IsShiftPressed(IKeyboard kb)
        => kb.IsKeyPressed(Key.ShiftLeft) || kb.IsKeyPressed(Key.ShiftRight);

    public void OnCharInput(char c)
    {
        if (!_chrome.AddressBarFocused) return;
        if (char.IsControl(c)) return;

        // If there's a selection, replace it
        if (_chrome.HasAddressSelection)
        {
            int selStart = Math.Min(_chrome.SelectionStart, _chrome.SelectionEnd);
            int selEnd = Math.Max(_chrome.SelectionStart, _chrome.SelectionEnd);
            _chrome.AddressText = _chrome.AddressText[..selStart] + _chrome.AddressText[selEnd..];
            _chrome.CursorPosition = selStart;
            _chrome.SelectionStart = -1;
            _chrome.SelectionEnd = -1;
        }

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
        bool wasDragging = _isDragging;
        _isDragging = false;
        _isAddressBarDragging = false;

        float scale = _getContentScale();
        float x = physicalX / scale;
        float y = physicalY / scale;

        float dx = x - _mouseDownX;
        float dy = y - _mouseDownY;
        bool isClick = (dx * dx + dy * dy) < ClickThreshold * ClickThreshold;

        if (y >= BrowserChrome.TotalChromeHeight)
        {
            var tab = _tabs.ActiveTab;
            if (tab?.LayoutRoot is not null)
            {
                float contentX = x;
                float contentY = y - BrowserChrome.TotalChromeHeight + tab.Scroll.ScrollY;

                // Dispatch mouseup event
                var hitBox = LayoutBoxHitTester.HitTest(tab.LayoutRoot, contentX, contentY);
                var mouseUpNode = hitBox?.DomNode;
                if (mouseUpNode is not null)
                {
                    mouseUpNode.DispatchEvent(new MouseEvent
                    {
                        Type = "mouseup", Bubbles = true, Cancelable = true,
                        ClientX = contentX, ClientY = contentY, Button = 0,
                    });
                }

                // Dispatch click if mousedown and mouseup target the same node (or ancestor)
                if (isClick && _mouseDownNode is not null)
                {
                    var clickTarget = _mouseDownNode;
                    clickTarget.DispatchEvent(new MouseEvent
                    {
                        Type = "click", Bubbles = true, Cancelable = true,
                        ClientX = contentX, ClientY = contentY, Button = 0,
                    });
                }

                // Link navigation (on click, not drag)
                if (isClick && tab.CurrentUri is not null)
                {
                    var anchor = LayoutBoxHitTester.FindAnchorAncestor(hitBox);
                    if (anchor is not null)
                    {
                        var href = anchor.GetAttribute(HtmlAttributeNames.Href);
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            var resolvedUri = UrlResolver.Resolve(href, tab.CurrentUri);
                            var target = anchor.GetAttribute(HtmlAttributeNames.Target);
                            if (target is not null && target.Equals("_blank", StringComparison.OrdinalIgnoreCase))
                            {
                                _tabs.CreateTab();
                            }
                            _navigateCallback(resolvedUri);
                            tab.Selection.Clear();
                        }
                    }
                }
            }
        }

        // Clear :active state on mouseup
        if (_currentActiveNode is not null)
        {
            InteractionStateHelper.ClearActive(_currentActiveNode);
            _currentActiveNode = null;
            var activeTab = _tabs.ActiveTab;
            if (activeTab?.Document is not null)
                activeTab.Document.NeedsLayout = true;
        }

        _mouseDownNode = null;
    }

    public void OnMouseMove(float physicalX, float physicalY, float viewportWidth)
    {
        float scale = _getContentScale();
        float x = physicalX / scale;
        float y = physicalY / scale;

        // Handle address bar drag selection
        if (_isAddressBarDragging)
        {
            int pos = HitTestAddressBarCursor(x);
            _chrome.SelectionEnd = pos;
            _chrome.CursorPosition = pos;
            return;
        }

        var tab = _tabs.ActiveTab;

        // Content area: hover tracking and mouse move events
        if (y >= BrowserChrome.TotalChromeHeight && tab?.LayoutRoot is not null)
        {
            float contentX = x;
            float contentY = y - BrowserChrome.TotalChromeHeight + (tab.Scroll?.ScrollY ?? 0);

            ProcessContentHover(tab, contentX, contentY);

            // Text selection drag
            if (_isDragging)
            {
                var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
                var textHit = TextHitTester.HitTest(allRuns, contentX, contentY, _measurer);
                if (textHit.HasValue)
                    tab.Selection.End = new TextPosition(textHit.Value.runIndex, textHit.Value.charOffset);
            }
        }
        else
        {
            // Mouse is in chrome area — clear content hover
            ClearContentHover(tab);

            // Continue text selection drag even when mouse is in chrome area
            if (_isDragging && tab?.LayoutRoot is not null)
            {
                float contentX = x;
                float contentY = y - BrowserChrome.TotalChromeHeight + (tab.Scroll?.ScrollY ?? 0);
                var allRuns = TextHitTester.CollectTextRuns(tab.LayoutRoot);
                var textHit = TextHitTester.HitTest(allRuns, contentX, contentY, _measurer);
                if (textHit.HasValue)
                    tab.Selection.End = new TextPosition(textHit.Value.runIndex, textHit.Value.charOffset);
            }
        }
    }

    private void ProcessContentHover(Tab tab, float contentX, float contentY)
    {
        var hitBox = LayoutBoxHitTester.HitTest(tab.LayoutRoot!, contentX, contentY);
        var newTarget = hitBox?.DomNode;

        if (!ReferenceEquals(newTarget, _currentHoverTarget))
        {
            DispatchHoverTransitionEvents(newTarget, contentX, contentY);
            InteractionStateHelper.UpdateHover(_currentHoverTarget, newTarget, tab.Document);
            _currentHoverTarget = newTarget;
        }

        // Always dispatch mousemove on current target (bubbling)
        newTarget?.DispatchEvent(new MouseEvent
        {
            Type = "mousemove", Bubbles = true, Cancelable = true,
            ClientX = contentX, ClientY = contentY,
        });
    }

    private void DispatchHoverTransitionEvents(Node? newTarget, float clientX, float clientY)
    {
        // Fire mouseout on old target (bubbling)
        _currentHoverTarget?.DispatchEvent(new MouseEvent
        {
            Type = "mouseout", Bubbles = true, Cancelable = true,
            ClientX = clientX, ClientY = clientY,
        });

        // Fire mouseleave on old ancestors NOT in new target's ancestor chain (non-bubbling)
        var oldAncestors = InteractionStateHelper.GetElementAncestors(_currentHoverTarget);
        var newAncestorSet = new HashSet<Element>(InteractionStateHelper.GetElementAncestors(newTarget));
        foreach (var el in oldAncestors)
        {
            if (!newAncestorSet.Contains(el))
            {
                el.DispatchEvent(new MouseEvent
                {
                    Type = "mouseleave", Bubbles = false, Cancelable = false,
                    ClientX = clientX, ClientY = clientY,
                });
            }
        }

        // Fire mouseover on new target (bubbling)
        newTarget?.DispatchEvent(new MouseEvent
        {
            Type = "mouseover", Bubbles = true, Cancelable = true,
            ClientX = clientX, ClientY = clientY,
        });

        // Fire mouseenter on new ancestors NOT in old target's ancestor chain (non-bubbling)
        var oldAncestorSet = new HashSet<Element>(oldAncestors);
        foreach (var el in InteractionStateHelper.GetElementAncestors(newTarget))
        {
            if (!oldAncestorSet.Contains(el))
            {
                el.DispatchEvent(new MouseEvent
                {
                    Type = "mouseenter", Bubbles = false, Cancelable = false,
                    ClientX = clientX, ClientY = clientY,
                });
            }
        }
    }

    private void ClearContentHover(Tab? tab)
    {
        if (_currentHoverTarget is not null)
        {
            InteractionStateHelper.UpdateHover(_currentHoverTarget, null, tab?.Document);
            _currentHoverTarget = null;
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
                _chrome.SelectionStart = 0;
                _chrome.SelectionEnd = _chrome.AddressText.Length;
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

        items.Add(new ContextMenuItem
        {
            Label = "",
            Action = () => { },
            IsSeparator = true,
        });

        items.Add(new ContextMenuItem
        {
            Label = "Developer Tools",
            Enabled = true,
            Action = () => _onToggleDevTools(),
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
        newTab.LoadHtmlDirect(sourceHtml);

        _chrome.AddressText = $"view-source:{tab.CurrentUri}";
        _chrome.CursorPosition = _chrome.AddressText.Length;
    }

    private static string GetDocumentHtml(SuperRender.Document.Dom.Document doc)
    {
        var sb = new System.Text.StringBuilder();
        SerializeNode(doc, sb);
        return sb.ToString();
    }

    private static void SerializeNode(SuperRender.Document.Dom.Node node, System.Text.StringBuilder sb)
    {
        if (node is SuperRender.Document.Dom.TextNode text)
        {
            sb.Append(text.Data);
            return;
        }

        if (node is SuperRender.Document.Dom.Element el)
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
}
