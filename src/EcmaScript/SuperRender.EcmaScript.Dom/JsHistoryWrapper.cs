using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

// JSGEN005/006/007: history.pushState/replaceState accept an arbitrary JS state value (pass-through
// JsValue) and the `state` getter returns whatever was stored. Typed migration would lose this
// arbitrary-payload semantics.
#pragma warning disable JSGEN005, JSGEN006, JSGEN007

/// <summary>
/// JS wrapper for window.history. Provides pushState/replaceState/back/forward/go.
/// Uses delegates so the EcmaScript.Dom project remains dependency-free.
/// </summary>
[JsObject(GenerateInterface = true)]
internal sealed partial class JsHistoryWrapper : JsObject
{
    private readonly List<HistoryEntry> _entries = [];
    private int _currentIndex = -1;
    private readonly Func<Uri?> _getCurrentUri;
    private readonly Action<string> _navigate;
    private readonly Action<string> _replace;
    private readonly Action _goBack;
    private readonly Action _goForward;
    private readonly Action<Uri> _updateAddressBar;

    public JsHistoryWrapper(
        Func<Uri?> getCurrentUri,
        Action<string> navigate,
        Action<string> replace,
        Action goBack,
        Action goForward,
        Action<Uri> updateAddressBar,
        Realm realm)
    {
        _getCurrentUri = getCurrentUri;
        _navigate = navigate;
        _replace = replace;
        _goBack = goBack;
        _goForward = goForward;
        _updateAddressBar = updateAddressBar;
        Prototype = realm.ObjectPrototype;

        var current = getCurrentUri();
        if (current is not null)
        {
            _entries.Add(new HistoryEntry { Url = current, State = JsValue.Null });
            _currentIndex = 0;
        }
    }

    [JsProperty("length")]
    public int Length => _entries.Count;

    [JsProperty("state")]
    public JsValue State
    {
        get
        {
            if (_currentIndex >= 0 && _currentIndex < _entries.Count)
                return _entries[_currentIndex].State;
            return JsValue.Null;
        }
    }

    [JsMethod("pushState")]
    public JsValue PushState(JsValue _, JsValue[] args)
    {
        var state = args.Length > 0 ? args[0] : JsValue.Null;
        var url = args.Length > 2 && args[2] is not JsUndefined && args[2] is not JsNull
            ? args[2].ToJsString()
            : null;

        var newUri = ResolveHistoryUrl(url);

        if (_currentIndex < _entries.Count - 1)
            _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);

        _entries.Add(new HistoryEntry { Url = newUri, State = state });
        _currentIndex = _entries.Count - 1;
        _updateAddressBar(newUri);
        return JsValue.Undefined;
    }

    [JsMethod("replaceState")]
    public JsValue ReplaceState(JsValue _, JsValue[] args)
    {
        var state = args.Length > 0 ? args[0] : JsValue.Null;
        var url = args.Length > 2 && args[2] is not JsUndefined && args[2] is not JsNull
            ? args[2].ToJsString()
            : null;

        var newUri = ResolveHistoryUrl(url);

        if (_currentIndex >= 0 && _currentIndex < _entries.Count)
        {
            _entries[_currentIndex] = new HistoryEntry { Url = newUri, State = state };
        }
        else
        {
            _entries.Add(new HistoryEntry { Url = newUri, State = state });
            _currentIndex = _entries.Count - 1;
        }

        _updateAddressBar(newUri);
        return JsValue.Undefined;
    }

    [JsMethod("back")]
    public void Back()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            _updateAddressBar(_entries[_currentIndex].Url);
        }
        else
        {
            _goBack();
        }
    }

    [JsMethod("forward")]
    public void Forward()
    {
        if (_currentIndex < _entries.Count - 1)
        {
            _currentIndex++;
            _updateAddressBar(_entries[_currentIndex].Url);
        }
        else
        {
            _goForward();
        }
    }

    [JsMethod("go")]
    public void Go(int delta)
    {
        if (delta == 0) return;

        int newIndex = _currentIndex + delta;
        if (newIndex >= 0 && newIndex < _entries.Count)
        {
            _currentIndex = newIndex;
            _updateAddressBar(_entries[_currentIndex].Url);
        }
        else if (delta < 0)
        {
            _goBack();
        }
        else
        {
            _goForward();
        }
    }

    private Uri ResolveHistoryUrl(string? url)
    {
        if (url is not null)
        {
            var baseUri = _getCurrentUri();
            if (Uri.TryCreate(baseUri, url, out var resolved))
                return resolved;
            return baseUri ?? new Uri("about:blank");
        }
        return _getCurrentUri() ?? new Uri("about:blank");
    }

    private sealed class HistoryEntry
    {
        public required Uri Url { get; init; }
        public required JsValue State { get; set; }
    }
}
