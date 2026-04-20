using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for window.location. Provides URL property access and navigation methods.
/// Uses delegates for navigation so the EcmaScript.Dom project remains dependency-free.
/// </summary>
internal sealed class JsLocationWrapper : JsDynamicObject
{
    private readonly Func<Uri?> _getCurrentUri;
    private readonly Action<string> _navigate;
    private readonly Action<string> _replace;
    private readonly Action _reload;

    public JsLocationWrapper(
        Func<Uri?> getCurrentUri,
        Action<string> navigate,
        Action<string> replace,
        Action reload,
        Realm realm)
    {
        _getCurrentUri = getCurrentUri;
        _navigate = navigate;
        _replace = replace;
        _reload = reload;
        Prototype = realm.ObjectPrototype;
        InstallProperties();
    }

    private void InstallProperties()
    {
        this.DefineGetterSetter("href",
            () => new JsString(_getCurrentUri()?.ToString() ?? ""),
            v => _navigate(v.ToJsString()));

        this.DefineGetter("protocol", () =>
        {
            var uri = _getCurrentUri();
            return new JsString(uri is not null ? uri.Scheme + ":" : ":");
        });

        this.DefineGetter("host", () =>
        {
            var uri = _getCurrentUri();
            if (uri is null) return new JsString("");
            var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
            return new JsString(uri.Host + port);
        });

        this.DefineGetter("hostname", () => new JsString(_getCurrentUri()?.Host ?? ""));

        this.DefineGetter("port", () =>
        {
            var uri = _getCurrentUri();
            if (uri is null || uri.IsDefaultPort) return new JsString("");
            return new JsString(uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

        this.DefineGetter("pathname", () => new JsString(_getCurrentUri()?.AbsolutePath ?? "/"));
        this.DefineGetter("search", () => new JsString(_getCurrentUri()?.Query ?? ""));
        this.DefineGetter("hash", () => new JsString(_getCurrentUri()?.Fragment ?? ""));

        this.DefineGetter("origin", () =>
        {
            var uri = _getCurrentUri();
            if (uri is null) return new JsString("");
            var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
            return new JsString($"{uri.Scheme}://{uri.Host}{port}");
        });

        this.DefineMethod("assign", 1, args =>
        {
            if (args.Length > 0) _navigate(args[0].ToJsString());
            return Undefined;
        });

        this.DefineMethod("replace", 1, args =>
        {
            if (args.Length > 0) _replace(args[0].ToJsString());
            return Undefined;
        });

        this.DefineMethod("reload", 0, _ =>
        {
            _reload();
            return Undefined;
        });

        this.DefineMethod("toString", 0, _ => new JsString(_getCurrentUri()?.ToString() ?? ""));
    }
}
