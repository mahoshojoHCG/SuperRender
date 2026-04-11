using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.EcmaScript.Engine;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// Entry point for installing DOM globals into a JS engine.
/// Creates the bridge between C# DOM objects and the JS runtime.
/// </summary>
public sealed class DomBridge
{
    private readonly JsEngine _engine;
    private readonly DomDocument _document;
    private readonly NodeWrapperCache _cache;
    private JsDocumentWrapper? _documentWrapper;
    private JsWindowWrapper? _windowWrapper;

    public TimerScheduler TimerQueue { get; } = new();

    // Optional delegates for browser features (injected from Browser layer)
    // Storage
    private Func<string, string?>? _localStorageGetItem;
    private Action<string, string>? _localStorageSetItem;
    private Action<string>? _localStorageRemoveItem;
    private Action? _localStorageClear;
    private Func<int, string?>? _localStorageKey;
    private Func<int>? _localStorageGetLength;

    private Func<string, string?>? _sessionStorageGetItem;
    private Action<string, string>? _sessionStorageSetItem;
    private Action<string>? _sessionStorageRemoveItem;
    private Action? _sessionStorageClear;
    private Func<int, string?>? _sessionStorageKey;
    private Func<int>? _sessionStorageGetLength;

    // Fetch
    private Func<string, string, IReadOnlyList<KeyValuePair<string, string>>?, string?, Task<FetchResult>>? _fetchAsync;
    private Action<Action>? _enqueueMainThread;

    // Location / History
    private Func<Uri?>? _getCurrentUri;
    private Action<string>? _navigate;
    private Action<string>? _navigateReplace;
    private Action? _reload;
    private Action? _goBack;
    private Action? _goForward;
    private Action<Uri>? _updateAddressBar;

    // Cookies
    private Func<string>? _getCookies;
    private Action<string>? _setCookie;

    public DomBridge(JsEngine engine, DomDocument document)
    {
        _engine = engine;
        _document = document;
        _cache = new NodeWrapperCache(engine.Realm);
    }

    /// <summary>
    /// Configure localStorage delegates.
    /// </summary>
    public void SetLocalStorage(
        Func<string, string?> getItem,
        Action<string, string> setItem,
        Action<string> removeItem,
        Action clear,
        Func<int, string?> key,
        Func<int> getLength)
    {
        _localStorageGetItem = getItem;
        _localStorageSetItem = setItem;
        _localStorageRemoveItem = removeItem;
        _localStorageClear = clear;
        _localStorageKey = key;
        _localStorageGetLength = getLength;
    }

    /// <summary>
    /// Configure sessionStorage delegates.
    /// </summary>
    public void SetSessionStorage(
        Func<string, string?> getItem,
        Action<string, string> setItem,
        Action<string> removeItem,
        Action clear,
        Func<int, string?> key,
        Func<int> getLength)
    {
        _sessionStorageGetItem = getItem;
        _sessionStorageSetItem = setItem;
        _sessionStorageRemoveItem = removeItem;
        _sessionStorageClear = clear;
        _sessionStorageKey = key;
        _sessionStorageGetLength = getLength;
    }

    /// <summary>
    /// Configure the fetch API delegate.
    /// </summary>
    public void SetFetch(
        Func<string, string, IReadOnlyList<KeyValuePair<string, string>>?, string?, Task<FetchResult>> fetchAsync,
        Action<Action> enqueueMainThread)
    {
        _fetchAsync = fetchAsync;
        _enqueueMainThread = enqueueMainThread;
    }

    /// <summary>
    /// Configure location and history delegates.
    /// </summary>
    public void SetLocationAndHistory(
        Func<Uri?> getCurrentUri,
        Action<string> navigate,
        Action<string> navigateReplace,
        Action reload,
        Action goBack,
        Action goForward,
        Action<Uri> updateAddressBar)
    {
        _getCurrentUri = getCurrentUri;
        _navigate = navigate;
        _navigateReplace = navigateReplace;
        _reload = reload;
        _goBack = goBack;
        _goForward = goForward;
        _updateAddressBar = updateAddressBar;
    }

    /// <summary>
    /// Configure cookie delegates.
    /// </summary>
    public void SetCookies(Func<string> getCookies, Action<string> setCookie)
    {
        _getCookies = getCookies;
        _setCookie = setCookie;
    }

    /// <summary>
    /// Installs 'document' and 'window' globals into the JS engine's scope.
    /// </summary>
    public void Install()
    {
        _documentWrapper = (JsDocumentWrapper)_cache.GetOrCreate(_document);

        // Install document.cookie if configured
        if (_getCookies is not null && _setCookie is not null)
            _documentWrapper.InstallCookie(_getCookies, _setCookie);

        _windowWrapper = new JsWindowWrapper(_documentWrapper, _engine.Realm, TimerQueue);

        // Install localStorage
        if (_localStorageGetItem is not null)
        {
            var localStorage = new JsStorageWrapper(
                _localStorageGetItem, _localStorageSetItem!, _localStorageRemoveItem!,
                _localStorageClear!, _localStorageKey!, _localStorageGetLength!,
                _engine.Realm);
            _windowWrapper.InstallStorage("localStorage", localStorage);
        }

        // Install sessionStorage
        if (_sessionStorageGetItem is not null)
        {
            var sessionStorage = new JsStorageWrapper(
                _sessionStorageGetItem, _sessionStorageSetItem!, _sessionStorageRemoveItem!,
                _sessionStorageClear!, _sessionStorageKey!, _sessionStorageGetLength!,
                _engine.Realm);
            _windowWrapper.InstallStorage("sessionStorage", sessionStorage);
        }

        // Install location
        if (_getCurrentUri is not null && _navigate is not null && _navigateReplace is not null && _reload is not null)
        {
            var location = new JsLocationWrapper(
                _getCurrentUri, _navigate, _navigateReplace, _reload, _engine.Realm);
            _windowWrapper.InstallLocation(location);
        }

        // Install history
        if (_getCurrentUri is not null && _navigate is not null && _navigateReplace is not null
            && _goBack is not null && _goForward is not null && _updateAddressBar is not null)
        {
            var history = new JsHistoryWrapper(
                _getCurrentUri, _navigate, _navigateReplace,
                _goBack, _goForward, _updateAddressBar, _engine.Realm);
            _windowWrapper.InstallHistory(history);
        }

        _engine.SetValue("document", _documentWrapper);
        _engine.SetValue("window", _windowWrapper);

        // Forward browser APIs from window to the global scope so scripts
        // can call setTimeout(...) directly without window. prefix
        foreach (var name in new[]
        {
            "setTimeout", "clearTimeout",
            "setInterval", "clearInterval",
            "requestAnimationFrame", "cancelAnimationFrame",
            "alert",
        })
        {
            if (_windowWrapper.HasProperty(name))
                _engine.SetValue(name, _windowWrapper.Get(name));
        }

        // Install fetch as a global function
        if (_fetchAsync is not null && _enqueueMainThread is not null)
        {
            var fetchFn = JsFetchApi.Create(_fetchAsync, _enqueueMainThread, _engine.Realm);
            _engine.SetValue("fetch", fetchFn);
            _windowWrapper.DefineOwnProperty("fetch",
                Runtime.PropertyDescriptor.Data(fetchFn));
        }

        // Forward location to global scope
        if (_windowWrapper.HasProperty("location"))
            _engine.SetValue("location", _windowWrapper.Get("location"));
    }

    /// <summary>
    /// Updates the window dimensions and device pixel ratio.
    /// Call this on resize events.
    /// </summary>
    public void UpdateWindowDimensions(float width, float height, float devicePixelRatio)
    {
        _windowWrapper?.UpdateDimensions(width, height, devicePixelRatio);
    }
}
