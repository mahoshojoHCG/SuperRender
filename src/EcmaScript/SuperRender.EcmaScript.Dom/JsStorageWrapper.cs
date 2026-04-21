using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

// JSGEN005/006: Storage.getItem returns string|null (should become JsOptional<string>) and key(i)
// returns the indexed key similarly; migration tracked separately with the rest of the DOM work.
#pragma warning disable JSGEN005, JSGEN006

/// <summary>
/// JS wrapper for the Web Storage API (localStorage / sessionStorage).
/// Wraps a WebStorage-like interface using delegates, so the EcmaScript.Dom project
/// remains dependency-free (no direct reference to Browser's WebStorage class).
/// </summary>
[JsObject(GenerateInterface = true)]
internal sealed partial class JsStorageWrapper : JsObject
{
    private readonly Func<string, string?> _getItem;
    private readonly Action<string, string> _setItem;
    private readonly Action<string> _removeItem;
    private readonly Action _clear;
    private readonly Func<int, string?> _key;
    private readonly Func<int> _getLength;

    public JsStorageWrapper(
        Func<string, string?> getItem,
        Action<string, string> setItem,
        Action<string> removeItem,
        Action clear,
        Func<int, string?> key,
        Func<int> getLength,
        Realm realm)
    {
        _getItem = getItem;
        _setItem = setItem;
        _removeItem = removeItem;
        _clear = clear;
        _key = key;
        _getLength = getLength;
        Prototype = realm.ObjectPrototype;
    }

    [JsMethod("getItem")]
    public JsValue GetItem(string key)
    {
        var result = _getItem(key);
        return result is not null ? new JsString(result) : JsValue.Null;
    }

    [JsMethod("setItem")]
    public void SetItem(string key, string value) => _setItem(key, value);

    [JsMethod("removeItem")]
    public void RemoveItem(string key) => _removeItem(key);

    [JsMethod("clear")]
    public void Clear() => _clear();

    [JsMethod("key")]
    public JsValue Key(int index)
    {
        var result = _key(index);
        return result is not null ? new JsString(result) : JsValue.Null;
    }

    [JsProperty("length")]
    public int Length => _getLength();
}
