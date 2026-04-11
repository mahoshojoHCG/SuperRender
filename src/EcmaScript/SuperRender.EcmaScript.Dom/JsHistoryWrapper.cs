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
        InstallProperties(realm);

        // Initialize with the current URI
        var current = getCurrentUri();
        if (current is not null)
        {
            _entries.Add(new HistoryEntry { Url = current, State = Null });
            _currentIndex = 0;
        }
    }

    private void InstallProperties(Realm realm)
    {
        DefineOwnProperty("length", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get length", (_, _) =>
                JsNumber.Create(_entries.Count), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("state", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get state", (_, _) =>
            {
                if (_currentIndex >= 0 && _currentIndex < _entries.Count)
                    return _entries[_currentIndex].State;
                return Null;
            }, 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("pushState", PropertyDescriptor.Data(
            JsFunction.CreateNative("pushState", (_, args) =>
            {
                var state = args.Length > 0 ? args[0] : Null;
                // title (args[1]) is ignored per spec
                var url = args.Length > 2 && args[2] is not JsUndefined && args[2] is not JsNull
                    ? args[2].ToJsString()
                    : null;

                Uri newUri;
                if (url is not null)
                {
                    var baseUri = _getCurrentUri();
                    if (Uri.TryCreate(baseUri, url, out var resolved))
                        newUri = resolved;
                    else
                        newUri = baseUri ?? new Uri("about:blank");
                }
                else
                {
                    newUri = _getCurrentUri() ?? new Uri("about:blank");
                }

                // Truncate forward entries
                if (_currentIndex < _entries.Count - 1)
                    _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);

                _entries.Add(new HistoryEntry { Url = newUri, State = state });
                _currentIndex = _entries.Count - 1;

                _updateAddressBar(newUri);

                return Undefined;
            }, 3)));

        DefineOwnProperty("replaceState", PropertyDescriptor.Data(
            JsFunction.CreateNative("replaceState", (_, args) =>
            {
                var state = args.Length > 0 ? args[0] : Null;
                var url = args.Length > 2 && args[2] is not JsUndefined && args[2] is not JsNull
                    ? args[2].ToJsString()
                    : null;

                Uri newUri;
                if (url is not null)
                {
                    var baseUri = _getCurrentUri();
                    if (Uri.TryCreate(baseUri, url, out var resolved))
                        newUri = resolved;
                    else
                        newUri = baseUri ?? new Uri("about:blank");
                }
                else
                {
                    newUri = _getCurrentUri() ?? new Uri("about:blank");
                }

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
            }, 3)));

        DefineOwnProperty("back", PropertyDescriptor.Data(
            JsFunction.CreateNative("back", (_, _) =>
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
            }, 0)));

        DefineOwnProperty("forward", PropertyDescriptor.Data(
            JsFunction.CreateNative("forward", (_, _) =>
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
            }, 0)));

        DefineOwnProperty("go", PropertyDescriptor.Data(
            JsFunction.CreateNative("go", (_, args) =>
            {
                var delta = args.Length > 0 ? (int)args[0].ToNumber() : 0;
                if (delta == 0)
                    return Undefined;

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
            }, 1)));
    }

    private sealed class HistoryEntry
    {
        public required Uri Url { get; init; }
        public required JsValue State { get; set; }
    }
}
