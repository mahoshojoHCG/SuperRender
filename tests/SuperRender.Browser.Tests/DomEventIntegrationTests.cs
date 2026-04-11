using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;
using Xunit;

namespace SuperRender.Browser.Tests;

public class DomEventIntegrationTests
{
    private static (JsEngine engine, DomDocument doc, DomBridge bridge) CreateTestEnvironment(string html)
    {
        var pipeline = new RenderPipeline(new SuperRender.Renderer.Rendering.Layout.MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var engine = new JsEngine();
        var bridge = new DomBridge(engine, doc);
        bridge.Install();
        return (engine, doc, bridge);
    }

    [Fact]
    public void AddEventListener_FiresOnDispatchEvent()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='target'>text</div></body></html>");
        engine.Execute(@"
            var fired = false;
            var el = document.getElementById('target');
            el.addEventListener('click', function(e) { fired = true; });
        ");

        // Dispatch event from C# side
        var target = doc.Body!.Children.OfType<Element>().First(e => e.Id == "target");
        target.DispatchEvent(new DomEvent { Type = "click" });

        var result = engine.Execute("fired");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void RemoveEventListener_StopsFiring()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='target'>text</div></body></html>");
        engine.Execute(@"
            var count = 0;
            var handler = function(e) { count++; };
            var el = document.getElementById('target');
            el.addEventListener('click', handler);
            el.removeEventListener('click', handler);
        ");

        var target = doc.Body!.Children.OfType<Element>().First(e => e.Id == "target");
        target.DispatchEvent(new DomEvent { Type = "click" });

        var result = engine.Execute("count");
        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void Event_BubblesFromChildToParent()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='parent'><span id='child'>text</span></div></body></html>");
        engine.Execute(@"
            var parentFired = false;
            document.getElementById('parent').addEventListener('click', function() { parentFired = true; });
        ");

        var child = doc.Body!.Children.OfType<Element>().First().Children.OfType<Element>().First();
        child.DispatchEvent(new DomEvent { Type = "click", Bubbles = true });

        var result = engine.Execute("parentFired");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void CapturePhase_FiresBeforeBubble()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='parent'><span id='child'>text</span></div></body></html>");
        engine.Execute(@"
            var order = [];
            document.getElementById('parent').addEventListener('click', function() { order.push('capture'); }, true);
            document.getElementById('parent').addEventListener('click', function() { order.push('bubble'); }, false);
        ");

        var child = doc.Body!.Children.OfType<Element>().First().Children.OfType<Element>().First();
        child.DispatchEvent(new DomEvent { Type = "click", Bubbles = true });

        var first = engine.Execute("order[0]");
        var second = engine.Execute("order[1]");
        Assert.Equal("capture", first.ToJsString());
        Assert.Equal("bubble", second.ToJsString());
    }

    [Fact]
    public void StopPropagation_PreventsParentHandler()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='parent'><span id='child'>text</span></div></body></html>");
        engine.Execute(@"
            var parentFired = false;
            var el = document.getElementById('child');
            el.addEventListener('click', function(e) { e.stopPropagation(); });
            document.getElementById('parent').addEventListener('click', function() { parentFired = true; });
        ");

        var child = doc.Body!.Children.OfType<Element>().First().Children.OfType<Element>().First();
        child.DispatchEvent(new DomEvent { Type = "click", Bubbles = true });

        var result = engine.Execute("parentFired");
        Assert.False(result.ToBoolean());
    }

    [Fact]
    public void PreventDefault_SetsDefaultPrevented()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='target'>text</div></body></html>");
        engine.Execute(@"
            var prevented = false;
            var el = document.getElementById('target');
            el.addEventListener('click', function(e) {
                e.preventDefault();
                prevented = e.defaultPrevented;
            });
        ");

        var target = doc.Body!.Children.OfType<Element>().First(e => e.Id == "target");
        target.DispatchEvent(new DomEvent { Type = "click", Cancelable = true });

        var result = engine.Execute("prevented");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void MultipleListeners_AllFire()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='target'>text</div></body></html>");
        engine.Execute(@"
            var count = 0;
            var el = document.getElementById('target');
            el.addEventListener('click', function() { count++; });
            el.addEventListener('click', function() { count++; });
            el.addEventListener('click', function() { count++; });
        ");

        var target = doc.Body!.Children.OfType<Element>().First(e => e.Id == "target");
        target.DispatchEvent(new DomEvent { Type = "click" });

        var result = engine.Execute("count");
        Assert.Equal(3.0, result.ToNumber());
    }

    [Fact]
    public void Event_Target_IsCorrect()
    {
        var (engine, doc, _) = CreateTestEnvironment("<html><body><div id='target'>text</div></body></html>");
        engine.Execute(@"
            var targetTag = '';
            var el = document.getElementById('target');
            el.addEventListener('click', function(e) { targetTag = e.target.tagName; });
        ");

        var target = doc.Body!.Children.OfType<Element>().First(e => e.Id == "target");
        target.DispatchEvent(new DomEvent { Type = "click" });

        var result = engine.Execute("targetTag");
        Assert.Equal("DIV", result.ToJsString());
    }
}
