using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// The 'window' global object exposed to JavaScript.
/// </summary>
internal sealed class JsWindowWrapper : JsObject
{
    private readonly JsObject _documentWrapper;
    private float _innerWidth;
    private float _innerHeight;
    private float _devicePixelRatio = 1.0f;
    private int _nextTimerId = 1;
    private readonly Dictionary<int, bool> _activeTimers = [];

    public JsWindowWrapper(JsObject documentWrapper, Realm realm)
    {
        _documentWrapper = documentWrapper;
        Prototype = realm.ObjectPrototype;
        InstallProperties(realm);
    }

    public void UpdateDimensions(float width, float height, float dpr)
    {
        _innerWidth = width;
        _innerHeight = height;
        _devicePixelRatio = dpr;
    }

    private void InstallProperties(Realm realm)
    {
        DefineOwnProperty("document", PropertyDescriptor.Data(_documentWrapper, writable: false));

        DefineOwnProperty("innerWidth", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get innerWidth", (_, _) => JsNumber.Create(_innerWidth), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("innerHeight", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get innerHeight", (_, _) => JsNumber.Create(_innerHeight), 0),
            null, enumerable: true, configurable: true));

        DefineOwnProperty("devicePixelRatio", PropertyDescriptor.Accessor(
            JsFunction.CreateNative("get devicePixelRatio", (_, _) => JsNumber.Create(_devicePixelRatio), 0),
            null, enumerable: true, configurable: true));

        // setTimeout/clearTimeout (stub - records timer but doesn't actually delay)
        DefineOwnProperty("setTimeout", PropertyDescriptor.Data(
            JsFunction.CreateNative("setTimeout", (_, args) =>
            {
                var id = _nextTimerId++;
                _activeTimers[id] = true;
                // In a real browser this would schedule async execution.
                // For now, execute immediately if delay is 0.
                if (args.Length > 0 && args[0] is JsFunction callback)
                {
                    var delay = args.Length > 1 ? args[1].ToNumber() : 0;
                    if (delay <= 0 && _activeTimers.ContainsKey(id))
                    {
                        _activeTimers.Remove(id);
                        callback.Call(Undefined, []);
                    }
                }
                return JsNumber.Create(id);
            }, 2)));

        DefineOwnProperty("clearTimeout", PropertyDescriptor.Data(
            JsFunction.CreateNative("clearTimeout", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var id = (int)args[0].ToNumber();
                    _activeTimers.Remove(id);
                }
                return Undefined;
            }, 1)));

        DefineOwnProperty("setInterval", PropertyDescriptor.Data(
            JsFunction.CreateNative("setInterval", (_, _) =>
            {
                // Stub: returns timer id but doesn't actually repeat
                return JsNumber.Create(_nextTimerId++);
            }, 2)));

        DefineOwnProperty("clearInterval", PropertyDescriptor.Data(
            JsFunction.CreateNative("clearInterval", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var id = (int)args[0].ToNumber();
                    _activeTimers.Remove(id);
                }
                return Undefined;
            }, 1)));

        DefineOwnProperty("alert", PropertyDescriptor.Data(
            JsFunction.CreateNative("alert", (_, args) =>
            {
                var msg = args.Length > 0 ? args[0].ToJsString() : "";
                Console.WriteLine($"[alert] {msg}");
                return Undefined;
            }, 1)));

        // Re-export console from the realm
        if (realm.GlobalObject.HasProperty("console"))
            DefineOwnProperty("console", PropertyDescriptor.Data(realm.GlobalObject.Get("console")));
    }
}
