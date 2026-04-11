using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// JS wrapper for window.location. Provides URL property access and navigation methods.
/// Uses delegates for navigation so the EcmaScript.Dom project remains dependency-free.
/// </summary>
internal sealed class JsLocationWrapper : JsObject
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
        InstallProperties(realm);
    }

    private void InstallProperties(Realm realm)
    {
        DefineOwnProperty("href", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get href", (_, _) =>
                new JsString(_getCurrentUri()?.ToString() ?? ""), 0),
            JsFunction.CreateNative("set href", (_, args) =>
            {
                if (args.Length > 0)
                    _navigate(args[0].ToJsString());
                return Undefined;
            }, 1),
            enumerable: true, configurable: true));

        DefineOwnProperty("protocol", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get protocol", (_, _) =>
            {
                var uri = _getCurrentUri();
                return new JsString(uri is not null ? uri.Scheme + ":" : ":");
            }, 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("host", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get host", (_, _) =>
            {
                var uri = _getCurrentUri();
                if (uri is null) return new JsString("");
                var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
                return new JsString(uri.Host + port);
            }, 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("hostname", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get hostname", (_, _) =>
                new JsString(_getCurrentUri()?.Host ?? ""), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("port", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get port", (_, _) =>
            {
                var uri = _getCurrentUri();
                if (uri is null || uri.IsDefaultPort) return new JsString("");
                return new JsString(uri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }, 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("pathname", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get pathname", (_, _) =>
                new JsString(_getCurrentUri()?.AbsolutePath ?? "/"), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("search", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get search", (_, _) =>
                new JsString(_getCurrentUri()?.Query ?? ""), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("hash", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get hash", (_, _) =>
                new JsString(_getCurrentUri()?.Fragment ?? ""), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("origin", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get origin", (_, _) =>
            {
                var uri = _getCurrentUri();
                if (uri is null) return new JsString("");
                var port = uri.IsDefaultPort ? "" : $":{uri.Port}";
                return new JsString($"{uri.Scheme}://{uri.Host}{port}");
            }, 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("assign", PropertyDescriptor.Data(
            JsFunction.CreateNative("assign", (_, args) =>
            {
                if (args.Length > 0)
                    _navigate(args[0].ToJsString());
                return Undefined;
            }, 1)));

        DefineOwnProperty("replace", PropertyDescriptor.Data(
            JsFunction.CreateNative("replace", (_, args) =>
            {
                if (args.Length > 0)
                    _replace(args[0].ToJsString());
                return Undefined;
            }, 1)));

        DefineOwnProperty("reload", PropertyDescriptor.Data(
            JsFunction.CreateNative("reload", (_, _) =>
            {
                _reload();
                return Undefined;
            }, 0)));

        DefineOwnProperty("toString", PropertyDescriptor.Data(
            JsFunction.CreateNative("toString", (_, _) =>
                new JsString(_getCurrentUri()?.ToString() ?? ""), 0)));
    }
}
