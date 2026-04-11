namespace SuperRender.Browser.Storage;

/// <summary>
/// localStorage implementation backed by SQLite. Persists across sessions, scoped by origin.
/// </summary>
public sealed class LocalStorage : WebStorage, IDisposable
{
    private readonly StorageDatabase _db;
    private readonly string _origin;

    public LocalStorage(StorageDatabase db, string origin)
    {
        _db = db;
        _origin = origin;
    }

    public override string? GetItem(string key) => _db.GetItem(_origin, key);
    public override void SetItem(string key, string value) => _db.SetItem(_origin, key, value);
    public override void RemoveItem(string key) => _db.RemoveItem(_origin, key);
    public override void Clear() => _db.Clear(_origin);

    public override string? Key(int index)
    {
        var keys = _db.GetKeys(_origin);
        return index >= 0 && index < keys.Count ? keys[index] : null;
    }

    public override int Length => _db.GetLength(_origin);

    public void Dispose()
    {
        // Database is shared, don't dispose it here
    }
}
