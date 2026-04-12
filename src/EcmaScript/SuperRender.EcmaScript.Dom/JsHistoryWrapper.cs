using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for window.history. Provides pushState/replaceState/back/forward/go.
/// Uses delegates so the EcmaScript.Dom project remains dependency-free.
/// </summary>
internal sealed class JsHistoryWrapper : JsObject
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
        InstallProperties();

        // Initialize with the current URI
        var current = getCurrentUri();
        if (current is not null)
        {
            _entries.Add(new HistoryEntry { Url = current, State = Null });
            _currentIndex = 0;
        }
    }

    private void InstallProperties()
    {
        this.DefineGetter("length", () => JsNumber.Create(_entries.Count));

        this.DefineGetter("state", () =>
        {
            if (_currentIndex >= 0 && _currentIndex < _entries.Count)
                return _entries[_currentIndex].State;
            return Null;
        });

        this.DefineMethod("pushState", 3, args =>
        {
            var state = args.Length > 0 ? args[0] : Null;
            var url = args.Length > 2 && args[2] is not JsUndefined && args[2] is not JsNull
                ? args[2].ToJsString()
                : null;

            var newUri = ResolveHistoryUrl(url);

            // Truncate forward entries
            if (_currentIndex < _entries.Count - 1)
                _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);

            _entries.Add(new HistoryEntry { Url = newUri, State = state });
            _currentIndex = _entries.Count - 1;
            _updateAddressBar(newUri);
            return Undefined;
        });

        this.DefineMethod("replaceState", 3, args =>
        {
            var state = args.Length > 0 ? args[0] : Null;
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
            return Undefined;
        });

        this.DefineMethod("back", 0, _ =>
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
            return Undefined;
        });

        this.DefineMethod("forward", 0, _ =>
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
            return Undefined;
        });

        this.DefineMethod("go", 1, args =>
        {
            var delta = args.Length > 0 ? (int)args[0].ToNumber() : 0;
            if (delta == 0) return Undefined;

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
            return Undefined;
        });
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
