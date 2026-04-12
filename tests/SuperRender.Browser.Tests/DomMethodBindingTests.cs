using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.EcmaScript.Dom;
using SuperRender.EcmaScript.Engine;
using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.Browser.Tests;

public class DomMethodBindingTests
{
    private static (JsEngine engine, DomDocument doc, DomBridge bridge) CreateTestEnvironment(string html)
        => TestEnvironmentHelper.Create(html);

    // --- replaceChild ---

    [Fact]
    public void ReplaceChild_ReturnsOldChild()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span id='old'>old</span></div></body></html>");
        var result = engine.Execute(@"
            var parent = document.getElementById('parent');
            var oldChild = document.getElementById('old');
            var newChild = document.createElement('em');
            var returned = parent.replaceChild(newChild, oldChild);
            returned.tagName;
        ");
        Assert.Equal("SPAN", result.ToJsString());
    }

    [Fact]
    public void ReplaceChild_NewChildIsInParent()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span id='old'>old</span></div></body></html>");
        var result = engine.Execute(@"
            var parent = document.getElementById('parent');
            var oldChild = document.getElementById('old');
            var newChild = document.createElement('em');
            parent.replaceChild(newChild, oldChild);
            parent.firstChild.tagName;
        ");
        Assert.Equal("EM", result.ToJsString());
    }

    // --- cloneNode ---

    [Fact]
    public void CloneNode_Deep_CopiesChildren()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='src'><span>child</span></div></body></html>");
        var result = engine.Execute(@"
            var src = document.getElementById('src');
            var clone = src.cloneNode(true);
            clone.childNodes.length;
        ");
        Assert.True(result.ToNumber() >= 1);
    }

    [Fact]
    public void CloneNode_Shallow_NoChildren()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='src'><span>child</span></div></body></html>");
        var result = engine.Execute(@"
            var src = document.getElementById('src');
            var clone = src.cloneNode(false);
            clone.childNodes.length;
        ");
        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void CloneNode_IsNotSameNode()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='src'>text</div></body></html>");
        var result = engine.Execute(@"
            var src = document.getElementById('src');
            var clone = src.cloneNode(true);
            src !== clone;
        ");
        Assert.True(result.ToBoolean());
    }

    // --- contains ---

    [Fact]
    public void Contains_ReturnsTrue_ForDescendant()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='outer'><span id='inner'>text</span></div></body></html>");
        var result = engine.Execute(@"
            var outer = document.getElementById('outer');
            var inner = document.getElementById('inner');
            outer.contains(inner);
        ");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Contains_ReturnsFalse_ForNonDescendant()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='a'>a</div><div id='b'>b</div></body></html>");
        var result = engine.Execute(@"
            var a = document.getElementById('a');
            var b = document.getElementById('b');
            a.contains(b);
        ");
        Assert.False(result.ToBoolean());
    }

    [Fact]
    public void Contains_ReturnsTrue_ForSelf()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.contains(el);
        ");
        Assert.True(result.ToBoolean());
    }

    // --- matches ---

    [Fact]
    public void Matches_ReturnsTrue_ForMatchingSelector()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el' class='foo'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.matches('.foo');
        ");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void Matches_ReturnsFalse_ForNonMatchingSelector()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el' class='foo'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.matches('.bar');
        ");
        Assert.False(result.ToBoolean());
    }

    // --- closest ---

    [Fact]
    public void Closest_FindsAncestor()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div class='wrapper'><span id='inner'>text</span></div></body></html>");
        var result = engine.Execute(@"
            var inner = document.getElementById('inner');
            var found = inner.closest('.wrapper');
            found.tagName;
        ");
        Assert.Equal("DIV", result.ToJsString());
    }

    [Fact]
    public void Closest_ReturnsNull_WhenNoMatch()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div><span id='inner'>text</span></div></body></html>");
        var result = engine.Execute(@"
            var inner = document.getElementById('inner');
            inner.closest('.nonexistent');
        ");
        Assert.Same(JsValue.Null, result);
    }

    [Fact]
    public void Closest_CanMatchSelf()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el' class='target'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.closest('.target').tagName;
        ");
        Assert.Equal("DIV", result.ToJsString());
    }

    // --- dataset ---

    [Fact]
    public void Dataset_ReturnsDataAttributes()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el' data-name='hello' data-value='42'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.dataset.name + ':' + el.dataset.value;
        ");
        Assert.Equal("hello:42", result.ToJsString());
    }

    [Fact]
    public void Dataset_CamelCasesKebab()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el' data-my-attr='yes'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.dataset.myAttr;
        ");
        Assert.Equal("yes", result.ToJsString());
    }

    // --- toggleAttribute ---

    [Fact]
    public void ToggleAttribute_AddsWhenMissing()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.toggleAttribute('hidden');
            el.hasAttribute('hidden');
        ");
        Assert.True(result.ToBoolean());
    }

    [Fact]
    public void ToggleAttribute_RemovesWhenPresent()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el' hidden>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.toggleAttribute('hidden');
            el.hasAttribute('hidden');
        ");
        Assert.False(result.ToBoolean());
    }

    [Fact]
    public void ToggleAttribute_ForceTrue_Ensures()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='el'>text</div></body></html>");
        var result = engine.Execute(@"
            var el = document.getElementById('el');
            el.toggleAttribute('disabled', true);
            el.hasAttribute('disabled');
        ");
        Assert.True(result.ToBoolean());
    }

    // --- firstElementChild / lastElementChild / childElementCount ---

    [Fact]
    public void FirstElementChild_ReturnsFirstElement()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span>a</span><em>b</em></div></body></html>");
        var result = engine.Execute(@"
            var parent = document.getElementById('parent');
            parent.firstElementChild.tagName;
        ");
        Assert.Equal("SPAN", result.ToJsString());
    }

    [Fact]
    public void LastElementChild_ReturnsLastElement()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span>a</span><em>b</em></div></body></html>");
        var result = engine.Execute(@"
            var parent = document.getElementById('parent');
            parent.lastElementChild.tagName;
        ");
        Assert.Equal("EM", result.ToJsString());
    }

    [Fact]
    public void ChildElementCount_ReturnsCount()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span>a</span><em>b</em></div></body></html>");
        var result = engine.Execute(@"
            var parent = document.getElementById('parent');
            parent.childElementCount;
        ");
        Assert.Equal(2.0, result.ToNumber());
    }

    // --- after ---

    [Fact]
    public void After_InsertsAfterElement()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span id='ref'>a</span></div></body></html>");
        var result = engine.Execute(@"
            var ref = document.getElementById('ref');
            var newEl = document.createElement('em');
            ref.after(newEl);
            document.getElementById('parent').lastChild.tagName;
        ");
        Assert.Equal("EM", result.ToJsString());
    }

    // --- before ---

    [Fact]
    public void Before_InsertsBeforeElement()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span id='ref'>a</span></div></body></html>");
        var result = engine.Execute(@"
            var ref = document.getElementById('ref');
            var newEl = document.createElement('em');
            ref.before(newEl);
            document.getElementById('parent').firstChild.tagName;
        ");
        Assert.Equal("EM", result.ToJsString());
    }

    // --- remove ---

    [Fact]
    public void Remove_RemovesFromParent()
    {
        var (engine, _, _) = CreateTestEnvironment(
            "<html><body><div id='parent'><span id='child'>text</span></div></body></html>");
        var result = engine.Execute(@"
            var child = document.getElementById('child');
            child.remove();
            document.getElementById('parent').childNodes.length;
        ");
        Assert.Equal(0.0, result.ToNumber());
    }

    [Fact]
    public void Remove_NoParent_DoesNotThrow()
    {
        var (engine, _, _) = CreateTestEnvironment("<html><body></body></html>");
        // Removing a detached element should not throw
        var result = engine.Execute(@"
            var el = document.createElement('div');
            el.remove();
            'ok';
        ");
        Assert.Equal("ok", result.ToJsString());
    }
}
