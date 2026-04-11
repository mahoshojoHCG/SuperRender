using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// The 'window' global object exposed to JavaScript.
/// </summary>
internal sealed class JsWindowWrapper : JsObject
{
    private readonly JsObject _documentWrapper;
    private readonly TimerScheduler _timerQueue;
    private float _innerWidth;
    private float _innerHeight;
    private float _devicePixelRatio = 1.0f;

    public JsWindowWrapper(JsObject documentWrapper, Realm realm, TimerScheduler timerQueue)
    {
        _documentWrapper = documentWrapper;
        _timerQueue = timerQueue;
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

        // setTimeout with real delay via TimerQueue
        DefineOwnProperty("setTimeout", PropertyDescriptor.Data(
            JsFunction.CreateNative("setTimeout", (_, args) =>
            {
                if (args.Length > 0 && args[0] is JsFunction callback)
                {
                    var delay = args.Length > 1 ? args[1].ToNumber() : 0;
                    var id = _timerQueue.SetTimeout(() => callback.Call(Undefined, []), delay);
                    return JsNumber.Create(id);
                }
                return JsNumber.Create(0);
            }, 2)));

        DefineOwnProperty("clearTimeout", PropertyDescriptor.Data(
            JsFunction.CreateNative("clearTimeout", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var id = (int)args[0].ToNumber();
                    _timerQueue.Cancel(id);
                }
                return Undefined;
            }, 1)));

        // setInterval with real repeating via TimerQueue
        DefineOwnProperty("setInterval", PropertyDescriptor.Data(
            JsFunction.CreateNative("setInterval", (_, args) =>
            {
                if (args.Length > 0 && args[0] is JsFunction callback)
                {
                    var interval = args.Length > 1 ? args[1].ToNumber() : 0;
                    var id = _timerQueue.SetInterval(() => callback.Call(Undefined, []), interval);
                    return JsNumber.Create(id);
                }
                return JsNumber.Create(0);
            }, 2)));

        DefineOwnProperty("clearInterval", PropertyDescriptor.Data(
            JsFunction.CreateNative("clearInterval", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var id = (int)args[0].ToNumber();
                    _timerQueue.Cancel(id);
                }
                return Undefined;
            }, 1)));

        // requestAnimationFrame tied to render loop
        DefineOwnProperty("requestAnimationFrame", PropertyDescriptor.Data(
            JsFunction.CreateNative("requestAnimationFrame", (_, args) =>
            {
                if (args.Length > 0 && args[0] is JsFunction callback)
                {
                    var id = _timerQueue.RequestAnimationFrame(
                        () => callback.Call(Undefined, [JsNumber.Create(_timerQueue.NowMs)]));
                    return JsNumber.Create(id);
                }
                return JsNumber.Create(0);
            }, 1)));

        DefineOwnProperty("cancelAnimationFrame", PropertyDescriptor.Data(
            JsFunction.CreateNative("cancelAnimationFrame", (_, args) =>
            {
                if (args.Length > 0)
                {
                    var id = (int)args[0].ToNumber();
                    _timerQueue.Cancel(id);
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
