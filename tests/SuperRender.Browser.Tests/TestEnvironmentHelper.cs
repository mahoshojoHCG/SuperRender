using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Renderer.Rendering;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;

namespace SuperRender.Browser.Tests;

/// <summary>
/// Shared test setup helper for creating a JsEngine + Document + DomBridge environment.
/// </summary>
internal static class TestEnvironmentHelper
{
    internal static (JsEngine engine, DomDocument doc, DomBridge bridge) Create(string html)
    {
        var pipeline = new RenderPipeline(new SuperRender.Renderer.Rendering.Layout.MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var engine = new JsEngine();
        var bridge = new DomBridge(engine, doc);
        bridge.Install();
        return (engine, doc, bridge);
    }
}
