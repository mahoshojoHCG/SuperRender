using System.Globalization;
using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for window.location. Provides URL property access and navigation methods.
/// Uses delegates for navigation so the EcmaScript.Dom project remains dependency-free.
/// </summary>
[JsObject(GenerateInterface = true)]
internal sealed partial class JsLocationWrapper : JsObject
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
    }

    [JsProperty("href")]
    public string Href => _getCurrentUri()?.ToString() ?? "";

    [JsProperty("href", IsSetter = true)]
    public void SetHref(string value) => _navigate(value);

    [JsProperty("protocol")]
    public string Protocol
    {
        get
        {
            var uri = _getCurrentUri();
            return uri is not null ? uri.Scheme + ":" : ":";
        }
    }

    [JsProperty("host")]
    public string Host
    {
        get
        {
            var uri = _getCurrentUri();
            if (uri is null) return "";
            var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
            return uri.Host + port;
        }
    }

    [JsProperty("hostname")]
    public string Hostname => _getCurrentUri()?.Host ?? "";

    [JsProperty("port")]
    public string Port
    {
        get
        {
            var uri = _getCurrentUri();
            if (uri is null || uri.IsDefaultPort) return "";
            return uri.Port.ToString(CultureInfo.InvariantCulture);
        }
    }

    [JsProperty("pathname")]
    public string Pathname => _getCurrentUri()?.AbsolutePath ?? "/";

    [JsProperty("search")]
    public string Search => _getCurrentUri()?.Query ?? "";

    [JsProperty("hash")]
    public string Hash => _getCurrentUri()?.Fragment ?? "";

    [JsProperty("origin")]
    public string Origin
    {
        get
        {
            var uri = _getCurrentUri();
            if (uri is null) return "";
            var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
            return $"{uri.Scheme}://{uri.Host}{port}";
        }
    }

    [JsMethod("assign")]
    public void Assign(string url) => _navigate(url);

    [JsMethod("replace")]
    public void Replace(string url) => _replace(url);

    [JsMethod("reload")]
    public void Reload() => _reload();

    [JsMethod("toString")]
    public string ToLocationString() => _getCurrentUri()?.ToString() ?? "";
}
