using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.NodeSimulator.Modules;

/// <summary>
/// Cross-platform (Windows/Linux/macOS) file watcher backed by
/// <see cref="FileSystemWatcher"/>. Emits Node-style "change"/"rename" events
/// via optional listener callback and own `on(event, fn)` API.
/// </summary>
public sealed class FsWatcherObject : JsDynamicObject, IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, List<JsFunction>> _listeners = new(StringComparer.Ordinal);
    private bool _closed;

    public FsWatcherObject(string path, bool recursive, JsFunction? listener, Realm realm)
    {
        bool isDir = Directory.Exists(path);
        var watchPath = isDir ? path : Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var watchFilter = isDir ? "*" : Path.GetFileName(path);
        _watcher = new FileSystemWatcher(watchPath, string.IsNullOrEmpty(watchFilter) ? "*" : watchFilter)
        {
            IncludeSubdirectories = recursive && isDir,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite | NotifyFilters.Size
                         | NotifyFilters.CreationTime | NotifyFilters.Attributes,
        };
        if (listener is not null)
        {
            On("change", listener);
        }
        _watcher.Changed += (_, e) => Dispatch("change", "change", e.Name);
        _watcher.Created += (_, e) => Dispatch("change", "rename", e.Name);
        _watcher.Deleted += (_, e) => Dispatch("change", "rename", e.Name);
        _watcher.Renamed += (_, e) => Dispatch("change", "rename", e.Name);
        _watcher.Error += (_, e) => Dispatch("error", e.GetException().Message, null);
        _watcher.EnableRaisingEvents = true;

        InstallMethods();
    }

    private void InstallMethods()
    {
        DefineMethod("on", 2, (_, args) =>
        {
            if (args.Length < 2 || args[0] is not JsString s || args[1] is not JsFunction fn) return this;
            On(s.Value, fn);
            return this;
        });
        DefineMethod("off", 2, (_, args) =>
        {
            if (args.Length < 2 || args[0] is not JsString s || args[1] is not JsFunction fn) return this;
            if (_listeners.TryGetValue(s.Value, out var list)) list.Remove(fn);
            return this;
        });
        DefineMethod("close", 0, (_, _) => { Dispose(); return JsValue.Undefined; });
        DefineMethod("ref", 0, (_, _) => this);
        DefineMethod("unref", 0, (_, _) => this);
    }

    private void DefineMethod(string name, int length, Func<JsValue, JsValue[], JsValue> impl)
    {
        DefineOwnProperty(name, PropertyDescriptor.Data(
            JsFunction.CreateNative(name, impl, length), writable: true, enumerable: false, configurable: true));
    }

    internal void On(string evt, JsFunction fn)
    {
        if (!_listeners.TryGetValue(evt, out var list))
        {
            list = new List<JsFunction>();
            _listeners[evt] = list;
        }
        list.Add(fn);
    }

    private void Dispatch(string evt, string type, string? name)
    {
        if (_closed) return;
        if (!_listeners.TryGetValue(evt, out var list) || list.Count == 0) return;
        JsValue[] args = name is null
            ? new JsValue[] { new JsString(type) }
            : new JsValue[] { new JsString(type), new JsString(name) };
        foreach (var fn in list.ToArray())
        {
            try { fn.Call(this, args); }
            catch { /* listener errors shouldn't crash the watcher thread */ }
        }
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        Dispatch("close", "close", null);
    }
}
