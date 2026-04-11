using SuperRender.Browser.Storage;
using Xunit;

namespace SuperRender.Browser.Tests;

public class WebStorageTests : IDisposable
{
    private readonly string _dbPath;
    private readonly StorageDatabase _db;

    public WebStorageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sr_test_{Guid.NewGuid():N}.db");
        _db = new StorageDatabase(_dbPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { File.Delete(_dbPath); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }

    // --- StorageDatabase Tests ---

    [Fact]
    public void StorageDatabase_SetItem_GetItem_Roundtrip()
    {
        _db.SetItem("origin1", "key1", "value1");
        Assert.Equal("value1", _db.GetItem("origin1", "key1"));
    }

    [Fact]
    public void StorageDatabase_GetItem_NotFound_ReturnsNull()
    {
        Assert.Null(_db.GetItem("origin1", "nonexistent"));
    }

    [Fact]
    public void StorageDatabase_SetItem_OverwritesExisting()
    {
        _db.SetItem("origin1", "key1", "old");
        _db.SetItem("origin1", "key1", "new");
        Assert.Equal("new", _db.GetItem("origin1", "key1"));
    }

    [Fact]
    public void StorageDatabase_RemoveItem_RemovesKey()
    {
        _db.SetItem("origin1", "key1", "value1");
        _db.RemoveItem("origin1", "key1");
        Assert.Null(_db.GetItem("origin1", "key1"));
    }

    [Fact]
    public void StorageDatabase_Clear_RemovesAllForOrigin()
    {
        _db.SetItem("origin1", "a", "1");
        _db.SetItem("origin1", "b", "2");
        _db.SetItem("origin2", "c", "3");
        _db.Clear("origin1");
        Assert.Equal(0, _db.GetLength("origin1"));
        Assert.Equal(1, _db.GetLength("origin2"));
    }

    [Fact]
    public void StorageDatabase_GetLength_ReturnsCorrectCount()
    {
        _db.SetItem("origin1", "a", "1");
        _db.SetItem("origin1", "b", "2");
        Assert.Equal(2, _db.GetLength("origin1"));
    }

    [Fact]
    public void StorageDatabase_GetKeys_ReturnsAllKeys()
    {
        _db.SetItem("origin1", "alpha", "1");
        _db.SetItem("origin1", "beta", "2");
        var keys = _db.GetKeys("origin1");
        Assert.Contains("alpha", keys);
        Assert.Contains("beta", keys);
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public void StorageDatabase_OriginIsolation_SeparateData()
    {
        _db.SetItem("origin1", "key", "val1");
        _db.SetItem("origin2", "key", "val2");
        Assert.Equal("val1", _db.GetItem("origin1", "key"));
        Assert.Equal("val2", _db.GetItem("origin2", "key"));
    }

    // --- LocalStorage Tests ---

    [Fact]
    public void LocalStorage_SetItem_GetItem()
    {
        var ls = new LocalStorage(_db, "http://example.com:80");
        ls.SetItem("foo", "bar");
        Assert.Equal("bar", ls.GetItem("foo"));
    }

    [Fact]
    public void LocalStorage_RemoveItem()
    {
        var ls = new LocalStorage(_db, "http://example.com:80");
        ls.SetItem("foo", "bar");
        ls.RemoveItem("foo");
        Assert.Null(ls.GetItem("foo"));
    }

    [Fact]
    public void LocalStorage_Clear()
    {
        var ls = new LocalStorage(_db, "http://example.com:80");
        ls.SetItem("a", "1");
        ls.SetItem("b", "2");
        ls.Clear();
        Assert.Equal(0, ls.Length);
    }

    [Fact]
    public void LocalStorage_Key_ReturnsKeyAtIndex()
    {
        var ls = new LocalStorage(_db, "http://key-test.com:80");
        ls.SetItem("first", "1");
        ls.SetItem("second", "2");
        // Keys are returned in insertion order
        Assert.NotNull(ls.Key(0));
        Assert.NotNull(ls.Key(1));
        Assert.Null(ls.Key(5));
    }

    [Fact]
    public void LocalStorage_Length_ReturnsItemCount()
    {
        var ls = new LocalStorage(_db, "http://length-test.com:80");
        Assert.Equal(0, ls.Length);
        ls.SetItem("a", "1");
        Assert.Equal(1, ls.Length);
    }

    // --- SessionStorage Tests ---

    [Fact]
    public void SessionStorage_SetItem_GetItem()
    {
        var ss = new SessionStorage();
        ss.SetItem("foo", "bar");
        Assert.Equal("bar", ss.GetItem("foo"));
    }

    [Fact]
    public void SessionStorage_RemoveItem()
    {
        var ss = new SessionStorage();
        ss.SetItem("foo", "bar");
        ss.RemoveItem("foo");
        Assert.Null(ss.GetItem("foo"));
    }

    [Fact]
    public void SessionStorage_Clear()
    {
        var ss = new SessionStorage();
        ss.SetItem("a", "1");
        ss.SetItem("b", "2");
        ss.Clear();
        Assert.Equal(0, ss.Length);
    }

    [Fact]
    public void SessionStorage_Key_ReturnsInOrder()
    {
        var ss = new SessionStorage();
        ss.SetItem("first", "1");
        ss.SetItem("second", "2");
        Assert.Equal("first", ss.Key(0));
        Assert.Equal("second", ss.Key(1));
        Assert.Null(ss.Key(2));
    }

    [Fact]
    public void SessionStorage_NotFound_ReturnsNull()
    {
        var ss = new SessionStorage();
        Assert.Null(ss.GetItem("nonexistent"));
    }
}
