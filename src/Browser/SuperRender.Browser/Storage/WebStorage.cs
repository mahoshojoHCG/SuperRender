namespace SuperRender.Browser.Storage;

/// <summary>
/// Abstract base for Web Storage API (localStorage, sessionStorage).
/// </summary>
public abstract class WebStorage
{
    public abstract string? GetItem(string key);
    public abstract void SetItem(string key, string value);
    public abstract void RemoveItem(string key);
    public abstract void Clear();
    public abstract string? Key(int index);
    public abstract int Length { get; }
}
