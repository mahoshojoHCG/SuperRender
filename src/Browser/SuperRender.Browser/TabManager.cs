using Microsoft.Extensions.Logging;
using SuperRender.Browser.Networking;
using SuperRender.Browser.Storage;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Browser;

/// <summary>
/// Manages browser tabs: creation, switching, and closing.
/// </summary>
public sealed class TabManager : IDisposable
{
    private readonly List<Tab> _tabs = [];
    private readonly ITextMeasurer _measurer;
    private readonly ResourceLoader _loader;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly Dictionary<string, SessionStorage> _sessionStorageByOrigin = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Tab> Tabs => _tabs;
    public int ActiveTabIndex { get; set; }
    public Tab? ActiveTab => _tabs.Count > 0 && ActiveTabIndex >= 0 && ActiveTabIndex < _tabs.Count
        ? _tabs[ActiveTabIndex] : null;

    // Shared browser-level resources
    public CookieJar? CookieJar { get; set; }
    public StorageDatabase? StorageDb { get; set; }
    public Action<Action>? EnqueueMainThread { get; set; }
    public ImageCache? ImageCache { get; set; }

    /// <summary>
    /// Callback invoked when a tab's URI changes via pushState/replaceState.
    /// Set by BrowserWindow to update the chrome address bar.
    /// </summary>
    public Action<Tab, Uri>? OnTabAddressBarChanged { get; set; }

    public TabManager(ITextMeasurer measurer, ResourceLoader loader, ILoggerFactory? loggerFactory = null)
    {
        _measurer = measurer;
        _loader = loader;
        _loggerFactory = loggerFactory;
    }

    public Tab CreateTab()
    {
        var tab = new Tab(_measurer, _loader, _loggerFactory?.CreateLogger<Tab>())
        {
            CookieJar = CookieJar,
            StorageDb = StorageDb,
            EnqueueMainThread = EnqueueMainThread,
            ImageCache = ImageCache,
        };
        tab.AddressBarChanged += uri =>
        {
            OnTabAddressBarChanged?.Invoke(tab, uri);
        };
        _tabs.Add(tab);
        ActiveTabIndex = _tabs.Count - 1;
        return tab;
    }

    public void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        _tabs[index].Dispose();
        _tabs.RemoveAt(index);

        if (_tabs.Count == 0)
        {
            // Always keep at least one tab
            CreateTab();
            return;
        }

        if (ActiveTabIndex >= _tabs.Count)
            ActiveTabIndex = _tabs.Count - 1;
    }

    public void SwitchTab(int index)
    {
        if (index >= 0 && index < _tabs.Count)
            ActiveTabIndex = index;
    }

    public async Task NavigateActiveTabAsync(Uri uri)
    {
        var tab = ActiveTab;
        if (tab is null) return;
        tab.SessionStorage = GetSessionStorage(uri);
        await tab.NavigateAsync(uri).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a shared SessionStorage instance for the given URI's origin.
    /// All tabs navigating to the same origin share the same session storage.
    /// </summary>
    public SessionStorage GetSessionStorage(Uri uri)
    {
        var origin = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        if (!_sessionStorageByOrigin.TryGetValue(origin, out var storage))
        {
            storage = new SessionStorage();
            _sessionStorageByOrigin[origin] = storage;
        }
        return storage;
    }

    public void Dispose()
    {
        foreach (var tab in _tabs)
            tab.Dispose();
        _tabs.Clear();
    }
}
