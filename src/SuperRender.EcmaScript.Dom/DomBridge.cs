using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Interop;

namespace SuperRender.EcmaScript.Dom;

/// <summary>
/// Entry point for installing DOM globals into a JS engine.
/// Creates the bridge between C# DOM objects and the JS runtime.
/// </summary>
public sealed class DomBridge
{
    private readonly JsEngine _engine;
    private readonly Document _document;
    private readonly NodeWrapperCache _cache;
    private JsDocumentWrapper? _documentWrapper;
    private JsWindowWrapper? _windowWrapper;

    public TimerScheduler TimerQueue { get; } = new();

    public DomBridge(JsEngine engine, Document document)
    {
        _engine = engine;
        _document = document;
        _cache = new NodeWrapperCache(engine.Realm);
    }

    /// <summary>
    /// Installs 'document' and 'window' globals into the JS engine's scope.
    /// </summary>
    public void Install()
    {
        _documentWrapper = (JsDocumentWrapper)_cache.GetOrCreate(_document);
        _windowWrapper = new JsWindowWrapper(_documentWrapper, _engine.Realm, TimerQueue);

        _engine.SetValue("document", _documentWrapper);
        _engine.SetValue("window", _windowWrapper);
    }

    /// <summary>
    /// Updates the window dimensions and device pixel ratio.
    /// Call this on resize events.
    /// </summary>
    public void UpdateWindowDimensions(float width, float height, float devicePixelRatio)
    {
        _windowWrapper?.UpdateDimensions(width, height, devicePixelRatio);
    }
}
