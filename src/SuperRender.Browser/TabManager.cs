using SuperRender.Browser.Networking;
using SuperRender.Core.Layout;

namespace SuperRender.Browser;

/// <summary>
/// Manages browser tabs: creation, switching, and closing.
/// </summary>
public sealed class TabManager : IDisposable
{
    private readonly List<Tab> _tabs = [];
    private readonly ITextMeasurer _measurer;
    private readonly ResourceLoader _loader;

    public IReadOnlyList<Tab> Tabs => _tabs;
    public int ActiveTabIndex { get; set; }
    public Tab? ActiveTab => _tabs.Count > 0 && ActiveTabIndex >= 0 && ActiveTabIndex < _tabs.Count
        ? _tabs[ActiveTabIndex] : null;

    public TabManager(ITextMeasurer measurer, ResourceLoader loader)
    {
        _measurer = measurer;
        _loader = loader;
    }

    public Tab CreateTab()
    {
        var tab = new Tab(_measurer, _loader);
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
        await tab.NavigateAsync(uri).ConfigureAwait(false);
    }

    public void Dispose()
    {
        foreach (var tab in _tabs)
            tab.Dispose();
        _tabs.Clear();
    }
}
