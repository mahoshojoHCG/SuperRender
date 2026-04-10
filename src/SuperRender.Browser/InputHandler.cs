using SuperRender.Browser.Networking;
using Silk.NET.Input;

namespace SuperRender.Browser;

/// <summary>
/// Routes keyboard and mouse input to the appropriate browser component.
/// </summary>
public sealed class InputHandler
{
    private readonly BrowserChrome _chrome;
    private readonly TabManager _tabs;
    private readonly Func<float> _getContentScale;
    private readonly Action<Uri> _navigateCallback;

    public InputHandler(
        BrowserChrome chrome,
        TabManager tabs,
        Func<float> getContentScale,
        Action<Uri> navigateCallback)
    {
        _chrome = chrome;
        _tabs = tabs;
        _getContentScale = getContentScale;
        _navigateCallback = navigateCallback;
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
                    _chrome.CursorPosition = _chrome.AddressText.Length;
                    break;

                case ChromeHitArea.GoButton:
                    TriggerNavigation();
                    break;

                case ChromeHitArea.ReloadButton:
                    if (_tabs.ActiveTab?.CurrentUri is not null)
                        _navigateCallback(_tabs.ActiveTab.CurrentUri);
                    break;
            }
        }
        else
        {
            // Click in content area - unfocus address bar
            _chrome.AddressBarFocused = false;
        }
    }

    public void OnKeyDown(Key key)
    {
        if (!_chrome.AddressBarFocused) return;

        switch (key)
        {
            case Key.Enter:
                TriggerNavigation();
                _chrome.AddressBarFocused = false;
                break;

            case Key.Escape:
                _chrome.AddressBarFocused = false;
                UpdateAddressFromTab();
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
}
