using SuperRender.EcmaScript.Runtime;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// The 'window' global object exposed to JavaScript.
/// </summary>
[JsObject]
internal sealed partial class JsWindowWrapper : JsObject
{
    private readonly JsObject _documentWrapper;
    private readonly TimerScheduler _timerQueue;
    private float _innerWidth;
    private float _innerHeight;
    private float _devicePixelRatio = 1.0f;
    private JsValue _console = JsValue.Undefined;
    private JsValue _localStorage = JsValue.Undefined;
    private JsValue _sessionStorage = JsValue.Undefined;
    private JsValue _location = JsValue.Undefined;
    private JsValue _history = JsValue.Undefined;
    private JsValue _fetch = JsValue.Undefined;

    public JsWindowWrapper(JsObject documentWrapper, Realm realm, TimerScheduler timerQueue)
    {
        _documentWrapper = documentWrapper;
        _timerQueue = timerQueue;
        Prototype = realm.ObjectPrototype;

        if (realm.GlobalObject.HasProperty("console"))
            _console = realm.GlobalObject.Get("console");
    }

    public void UpdateDimensions(float width, float height, float dpr)
    {
        _innerWidth = width;
        _innerHeight = height;
        _devicePixelRatio = dpr;
    }

    public void InstallStorage(string name, JsObject storageWrapper)
    {
        if (name == "localStorage")
            _localStorage = storageWrapper;
        else if (name == "sessionStorage")
            _sessionStorage = storageWrapper;
    }

    public void InstallLocation(JsObject locationWrapper)
    {
        _location = locationWrapper;
    }

    public void InstallHistory(JsObject historyWrapper)
    {
        _history = historyWrapper;
    }

    public void InstallFetch(JsValue fetchFn)
    {
        _fetch = fetchFn;
    }

#pragma warning disable JSGEN006 // dynamic install — value may be undefined or any JsObject subclass
    [JsProperty("console")]
    public JsValue JsConsole => _console;

    [JsProperty("localStorage")]
    public JsValue LocalStorage => _localStorage;

    [JsProperty("sessionStorage")]
    public JsValue SessionStorage => _sessionStorage;

    [JsProperty("location")]
    public JsValue Location => _location;

    [JsProperty("history")]
    public JsValue History => _history;

    [JsProperty("fetch")]
    public JsValue Fetch => _fetch;
#pragma warning restore JSGEN006

#pragma warning disable JSGEN006 // returns wrapped DOM node — needs IJsNode/IJsElement IJsType
    [JsProperty("document")]
    public JsValue Document => _documentWrapper;
#pragma warning restore JSGEN006

    [JsProperty("innerWidth")] public double InnerWidth => _innerWidth;
    [JsProperty("innerHeight")] public double InnerHeight => _innerHeight;
    [JsProperty("devicePixelRatio")] public double DevicePixelRatio => _devicePixelRatio;

#pragma warning disable JSGEN005 // variadic: callback + optional delay
    [JsMethod("setTimeout")]
    public int SetTimeout(JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            var delay = args.Length > 1 ? args[1].ToNumber() : 0;
            return _timerQueue.SetTimeout(() => callback.Call(JsValue.Undefined, []), delay);
        }
        return 0;
    }
#pragma warning restore JSGEN005

    [JsMethod("clearTimeout")]
    public void ClearTimeout(int id) => _timerQueue.Cancel(id);

#pragma warning disable JSGEN005 // variadic: callback + optional interval
    [JsMethod("setInterval")]
    public int SetInterval(JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            var interval = args.Length > 1 ? args[1].ToNumber() : 0;
            return _timerQueue.SetInterval(() => callback.Call(JsValue.Undefined, []), interval);
        }
        return 0;
    }
#pragma warning restore JSGEN005

    [JsMethod("clearInterval")]
    public void ClearInterval(int id) => _timerQueue.Cancel(id);

#pragma warning disable JSGEN005 // variadic: callback
    [JsMethod("requestAnimationFrame")]
    public int RequestAnimationFrame(JsValue[] args)
    {
        if (args.Length > 0 && args[0] is JsFunction callback)
        {
            return _timerQueue.RequestAnimationFrame(
                () => callback.Call(JsValue.Undefined, [JsNumber.Create(_timerQueue.NowMs)]));
        }
        return 0;
    }
#pragma warning restore JSGEN005

    [JsMethod("cancelAnimationFrame")]
    public void CancelAnimationFrame(int id) => _timerQueue.Cancel(id);

    [JsMethod("alert")]
    public static void Alert(string msg) => System.Console.WriteLine($"[alert] {msg}");
}
