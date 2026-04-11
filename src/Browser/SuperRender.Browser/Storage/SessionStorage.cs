namespace SuperRender.Browser.Storage;

/// <summary>
/// sessionStorage implementation backed by an in-memory dictionary.
/// Per-tab, cleared when the tab is closed.
/// </summary>
public sealed class SessionStorage : WebStorage
{
    private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);
    private readonly List<string> _keyOrder = [];

    public override string? GetItem(string key)
    {
        return _data.GetValueOrDefault(key);
    }

    public override void SetItem(string key, string value)
    {
        if (!_data.ContainsKey(key))
            _keyOrder.Add(key);
        _data[key] = value;
    }

    public override void RemoveItem(string key)
    {
        if (_data.Remove(key))
            _keyOrder.Remove(key);
    }

    public override void Clear()
    {
        _data.Clear();
        _keyOrder.Clear();
    }

    public override string? Key(int index)
    {
        return index >= 0 && index < _keyOrder.Count ? _keyOrder[index] : null;
    }

    public override int Length => _data.Count;
}
