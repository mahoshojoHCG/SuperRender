using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// The 'window' global object exposed to JavaScript.
/// </summary>
[JsObject]
internal sealed partial class JsWindowWrapper : JsDynamicObject
{
    private readonly JsDynamicObject _documentWrapper;
    private readonly TimerScheduler _timerQueue;
    private float _innerWidth;
    private float _innerHeight;
    private float _devicePixelRatio = 1.0f;

    public JsWindowWrapper(JsDynamicObject documentWrapper, Realm realm, TimerScheduler timerQueue)
    {
        _documentWrapper = documentWrapper;
        _timerQueue = timerQueue;
        Prototype = realm.ObjectPrototype;

        if (realm.GlobalObject.HasProperty("console"))
            DefineOwnProperty("console", PropertyDescriptor.Data(realm.GlobalObject.Get("console")));
    }

    public void UpdateDimensions(float width, float height, float dpr)
    {
        _innerWidth = width;
        _innerHeight = height;
        _devicePixelRatio = dpr;
    }

    public void InstallStorage(string name, JsObject storageWrapper)
    {
        DefineOwnProperty(name, PropertyDescriptor.Data(storageWrapper));
    }

    public void InstallLocation(JsObject locationWrapper)
    {
        DefineOwnProperty("location", PropertyDescriptor.Data(locationWrapper));
    }

    public void InstallHistory(JsObject historyWrapper)
    {
        DefineOwnProperty("history", PropertyDescriptor.Data(historyWrapper));
    }

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsProperty("document")]
    public JsValue Document => _documentWrapper;
#pragma warning restore JSGEN006

    [JsProperty("innerWidth")] public double InnerWidth => _innerWidth;
    [JsProperty("innerHeight")] public double InnerHeight => _innerHeight;
    [JsProperty("devicePixelRatio")] public double DevicePixelRatio => _devicePixelRatio;

#pragma warning disable JSGEN005 // legacy variadic: callback + optional args
#pragma warning disable JSGEN006 // legacy variadic: callback + optional args
#pragma warning disable JSGEN007 // legacy variadic: callback + optional args
    [JsMethod("setTimeout")]
    public JsValue SetTimeout(JsValue _, JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            var delay = args.Length > 1 ? args[1].ToNumber() : 0;
            var id = _timerQueue.SetTimeout(() => callback.Call(JsValue.Undefined, []), delay);
            return JsNumber.Create(id);
        }
        return JsNumber.Create(0);
    }
#pragma warning restore JSGEN007
#pragma warning restore JSGEN006
#pragma warning restore JSGEN005

    [JsMethod("clearTimeout")]
    public void ClearTimeout(int id) => _timerQueue.Cancel(id);

#pragma warning disable JSGEN005 // legacy variadic: callback + optional args
#pragma warning disable JSGEN006 // legacy variadic: callback + optional args
#pragma warning disable JSGEN007 // legacy variadic: callback + optional args
    [JsMethod("setInterval")]
    public JsValue SetInterval(JsValue _, JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            var interval = args.Length > 1 ? args[1].ToNumber() : 0;
            var id = _timerQueue.SetInterval(() => callback.Call(JsValue.Undefined, []), interval);
            return JsNumber.Create(id);
        }
        return JsNumber.Create(0);
    }
#pragma warning restore JSGEN007
#pragma warning restore JSGEN006
#pragma warning restore JSGEN005

    [JsMethod("clearInterval")]
    public void ClearInterval(int id) => _timerQueue.Cancel(id);

#pragma warning disable JSGEN005 // legacy variadic: callback + optional args
#pragma warning disable JSGEN006 // legacy variadic: callback + optional args
#pragma warning disable JSGEN007 // legacy variadic: callback + optional args
    [JsMethod("requestAnimationFrame")]
    public JsValue RequestAnimationFrame(JsValue _, JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            var id = _timerQueue.RequestAnimationFrame(
                () => callback.Call(JsValue.Undefined, [JsNumber.Create(_timerQueue.NowMs)]));
            return JsNumber.Create(id);
        }
        return JsNumber.Create(0);
    }
#pragma warning restore JSGEN007
#pragma warning restore JSGEN006
#pragma warning restore JSGEN005

    [JsMethod("cancelAnimationFrame")]
    public void CancelAnimationFrame(int id) => _timerQueue.Cancel(id);

    [JsMethod("alert")]
    public static void Alert(string msg) => Console.WriteLine($"[alert] {msg}");
}
