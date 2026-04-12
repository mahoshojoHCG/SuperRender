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

    /// <summary>
    /// Install a storage object (localStorage or sessionStorage) as a window property.
    /// </summary>
    public void InstallStorage(string name, JsObject storageWrapper)
    {
        DefineOwnProperty(name, PropertyDescriptor.Data(storageWrapper));
    }

    /// <summary>
    /// Install the location object as a window property.
    /// </summary>
    public void InstallLocation(JsObject locationWrapper)
    {
        DefineOwnProperty("location", PropertyDescriptor.Data(locationWrapper));
    }

    /// <summary>
    /// Install the history object as a window property.
    /// </summary>
    public void InstallHistory(JsObject historyWrapper)
    {
        DefineOwnProperty("history", PropertyDescriptor.Data(historyWrapper));
    }

    private void InstallProperties(Realm realm)
    {
        DefineOwnProperty("document", PropertyDescriptor.Data(_documentWrapper, writable: false));

        this.DefineGetter("innerWidth", () => JsNumber.Create(_innerWidth));
        this.DefineGetter("innerHeight", () => JsNumber.Create(_innerHeight));
        this.DefineGetter("devicePixelRatio", () => JsNumber.Create(_devicePixelRatio));

        // setTimeout with real delay via TimerQueue
        this.DefineMethod("setTimeout", 2, args =>
        {
            if (args.Length > 0 && args[0] is JsFunction callback)
            {
                var delay = args.Length > 1 ? args[1].ToNumber() : 0;
                var id = _timerQueue.SetTimeout(() => callback.Call(Undefined, []), delay);
                return JsNumber.Create(id);
            }
            return JsNumber.Create(0);
        });

        this.DefineMethod("clearTimeout", 1, args =>
        {
            if (args.Length > 0)
            {
                var id = (int)args[0].ToNumber();
                _timerQueue.Cancel(id);
            }
            return Undefined;
        });

        // setInterval with real repeating via TimerQueue
        this.DefineMethod("setInterval", 2, args =>
        {
            if (args.Length > 0 && args[0] is JsFunction callback)
            {
                var interval = args.Length > 1 ? args[1].ToNumber() : 0;
                var id = _timerQueue.SetInterval(() => callback.Call(Undefined, []), interval);
                return JsNumber.Create(id);
            }
            return JsNumber.Create(0);
        });

        this.DefineMethod("clearInterval", 1, args =>
        {
            if (args.Length > 0)
            {
                var id = (int)args[0].ToNumber();
                _timerQueue.Cancel(id);
            }
            return Undefined;
        });

        // requestAnimationFrame tied to render loop
        this.DefineMethod("requestAnimationFrame", 1, args =>
        {
            if (args.Length > 0 && args[0] is JsFunction callback)
            {
                var id = _timerQueue.RequestAnimationFrame(
                    () => callback.Call(Undefined, [JsNumber.Create(_timerQueue.NowMs)]));
                return JsNumber.Create(id);
            }
            return JsNumber.Create(0);
        });

        this.DefineMethod("cancelAnimationFrame", 1, args =>
        {
            if (args.Length > 0)
            {
                var id = (int)args[0].ToNumber();
                _timerQueue.Cancel(id);
            }
            return Undefined;
        });

        this.DefineMethod("alert", 1, args =>
        {
            var msg = args.Length > 0 ? args[0].ToJsString() : "";
            Console.WriteLine($"[alert] {msg}");
            return Undefined;
        });

        // Re-export console from the realm
        if (realm.GlobalObject.HasProperty("console"))
            DefineOwnProperty("console", PropertyDescriptor.Data(realm.GlobalObject.Get("console")));
    }
}
