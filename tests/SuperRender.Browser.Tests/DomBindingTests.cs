using SuperRender.Core;
using SuperRender.Core.Dom;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Interop;
using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.Browser.Tests;

public class DomBindingTests
{
    private static (JsEngine engine, Document doc, DomBridge bridge) CreateTestEnvironment(string html)
    {
        var pipeline = new RenderPipeline(new SuperRender.Core.Layout.MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);
        var engine = new JsEngine();
        var bridge = new DomBridge(engine, doc);
        bridge.Install();
        return (engine, doc, bridge);
    }

    private const string BasicHtml = "<html><head><title>Test</title></head><body><div id='test' class='foo bar'>hello</div></body></html>";

    [Fact]
    public void Document_Body_Exists()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.body !== null");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Document_Head_Exists()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.head !== null");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Document_CreateElement_ReturnsElement()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.createElement('span').tagName");
        Assert.Equal("SPAN", result.ToJsString());
    }

    [Fact]
    public void Document_GetElementById_FindsElement()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').tagName");
        Assert.Equal("DIV", result.ToJsString());
    }

    [Fact]
    public void Document_GetElementById_NotFound_ReturnsNull()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('nonexistent')");
        Assert.Same(JsValue.Null, result);
    }

    [Fact]
    public void Document_GetElementsByTagName_ReturnsCollection()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementsByTagName('div').length");
        Assert.True(result.ToNumber() >= 1);
    }

    [Fact]
    public void Document_QuerySelector_Works()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.querySelector('#test').tagName");
        Assert.Equal("DIV", result.ToJsString());
    }

    [Fact]
    public void Document_QuerySelectorAll_ReturnsList()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.querySelectorAll('div').length");
        Assert.True(result.ToNumber() >= 1);
    }

    [Fact]
    public void Element_TagName_IsUppercase()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').tagName");
        Assert.Equal("DIV", result.ToJsString());
    }

    [Fact]
    public void Element_Id_Getter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').id");
        Assert.Equal("test", result.ToJsString());
    }

    [Fact]
    public void Element_Id_Setter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute("document.getElementById('test').id = 'changed'");
        var result = engine.Execute("document.getElementById('changed').tagName");
        Assert.Equal("DIV", result.ToJsString());
    }

    [Fact]
    public void Element_ClassName_Getter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').className");
        Assert.Equal("foo bar", result.ToJsString());
    }

    [Fact]
    public void Element_ClassName_Setter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute("document.getElementById('test').className = 'baz'");
        var result = engine.Execute("document.getElementById('test').className");
        Assert.Equal("baz", result.ToJsString());
    }

    [Fact]
    public void Element_ClassList_Add()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute("document.getElementById('test').classList.add('newclass')");
        var result = engine.Execute("document.getElementById('test').classList.contains('newclass')");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Element_ClassList_Remove()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute("document.getElementById('test').classList.remove('foo')");
        var result = engine.Execute("document.getElementById('test').classList.contains('foo')");
        Assert.False(result.ToBoolean());
    }

    [Fact]
    public void Element_ClassList_Toggle()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        // Toggle off existing class
        engine.Execute("document.getElementById('test').classList.toggle('foo')");
        var result = engine.Execute("document.getElementById('test').classList.contains('foo')");
        Assert.False(result.ToBoolean());
    }

    [Fact]
    public void Element_ClassList_Contains()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').classList.contains('foo')");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Element_GetAttribute()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').getAttribute('id')");
        Assert.Equal("test", result.ToJsString());
    }

    [Fact]
    public void Element_SetAttribute()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute("document.getElementById('test').setAttribute('data-x', 'hello')");
        var result = engine.Execute("document.getElementById('test').getAttribute('data-x')");
        Assert.Equal("hello", result.ToJsString());
    }

    [Fact]
    public void Element_InnerHTML_Getter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').innerHTML");
        Assert.Contains("hello", result.ToJsString());
    }

    [Fact]
    public void Element_Style_Access()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        // Just verify style is accessible without throwing
        var result = engine.Execute("typeof document.getElementById('test').style");
        Assert.Equal("object", result.ToJsString());
    }

    [Fact]
    public void Node_AppendChild_AddsChild()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute(@"
            var el = document.createElement('span');
            document.getElementById('test').appendChild(el);
        ");
        var result = engine.Execute("document.getElementById('test').childNodes.length");
        Assert.True(result.ToNumber() >= 2);
    }

    [Fact]
    public void Node_RemoveChild_RemovesChild()
    {
        var (engine, _, _) = CreateTestEnvironment("<html><body><div id='parent'><span id='child'>x</span></div></body></html>");
        engine.Execute(@"
            var parent = document.getElementById('parent');
            var child = document.getElementById('child');
            parent.removeChild(child);
        ");
        var result = engine.Execute("document.getElementById('parent').childNodes.length");
        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void Node_ChildNodes_ReturnsChildren()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').childNodes.length");
        Assert.True(result.ToNumber() >= 1);
    }

    [Fact]
    public void Node_TextContent_Getter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("document.getElementById('test').textContent");
        Assert.Equal("hello", result.ToJsString());
    }

    [Fact]
    public void Node_TextContent_Setter()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        engine.Execute("document.getElementById('test').textContent = 'world'");
        var result = engine.Execute("document.getElementById('test').textContent");
        Assert.Equal("world", result.ToJsString());
    }

    [Fact]
    public void Window_Document_Reference()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("window.document === document");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Window_InnerWidth_IsNumber()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("typeof window.innerWidth");
        Assert.Equal("number", result.ToJsString());
    }

    [Fact]
    public void Window_InnerHeight_IsNumber()
    {
        var (engine, _, _) = CreateTestEnvironment(BasicHtml);
        var result = engine.Execute("typeof window.innerHeight");
        Assert.Equal("number", result.ToJsString());
    }
}
