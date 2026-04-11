using SuperRender.Document.Dom;
using Xunit;
using DomDocument = SuperRender.Document.Dom.Document;

namespace SuperRender.Browser.Tests;

public class MouseEventTests
{
    private static (DomDocument doc, Element div, Element span) CreateNestedDom()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var div = new Element("div");
        var span = new Element("span");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(div);
        div.AppendChild(span);
        return (doc, div, span);
    }

    [Fact]
    public void SetHovered_ElementAndAncestors_AllMarked()
    {
        var (_, div, span) = CreateNestedDom();
        InteractionStateHelper.SetHovered(span);
        Assert.True(span.IsHovered);
        Assert.True(div.IsHovered);
    }

    [Fact]
    public void ClearHovered_ElementAndAncestors_AllCleared()
    {
        var (_, div, span) = CreateNestedDom();
        InteractionStateHelper.SetHovered(span);
        InteractionStateHelper.ClearHovered(span);
        Assert.False(span.IsHovered);
        Assert.False(div.IsHovered);
    }

    [Fact]
    public void HoverChange_OldTargetCleared_NewTargetSet()
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var div1 = new Element("div");
        var div2 = new Element("div");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(div1);
        body.AppendChild(div2);

        InteractionStateHelper.UpdateHover(null, div1, doc);
        Assert.True(div1.IsHovered);

        InteractionStateHelper.UpdateHover(div1, div2, doc);
        Assert.False(div1.IsHovered);
        Assert.True(div2.IsHovered);
    }

    [Fact]
    public void Active_OnMouseDown_SetOnElement()
    {
        var (_, _, span) = CreateNestedDom();
        InteractionStateHelper.SetActive(span);
        Assert.True(span.IsActive);
    }

    [Fact]
    public void Active_OnMouseUp_ClearedOnAll()
    {
        var (_, div, span) = CreateNestedDom();
        InteractionStateHelper.SetActive(span);
        InteractionStateHelper.ClearActive(span);
        Assert.False(span.IsActive);
    }

    [Fact]
    public void Focus_OnClick_SetOnElement()
    {
        var a = new Element("a");
        InteractionStateHelper.SetFocus(a, null);
        Assert.True(a.IsFocused);
    }

    [Fact]
    public void Focus_Change_OldCleared()
    {
        var a1 = new Element("a");
        var a2 = new Element("a");
        InteractionStateHelper.SetFocus(a1, null);
        InteractionStateHelper.SetFocus(a2, a1);
        Assert.False(a1.IsFocused);
        Assert.True(a2.IsFocused);
    }

    [Fact]
    public void IsHovered_InheritedByAncestors()
    {
        var (_, div, span) = CreateNestedDom();
        InteractionStateHelper.SetHovered(span);
        Assert.True(div.IsHovered);
    }

    [Fact]
    public void MouseEvent_Created_WithCoordinates()
    {
        var evt = new MouseEvent { Type = "click", Bubbles = true, Cancelable = true, ClientX = 100, ClientY = 200 };
        Assert.Equal("click", evt.Type);
        Assert.Equal(100, evt.ClientX);
        Assert.Equal(200, evt.ClientY);
    }

    [Fact]
    public void HoverChange_TriggersNeedsLayout()
    {
        var (doc, _, span) = CreateNestedDom();
        doc.NeedsLayout = false;
        InteractionStateHelper.UpdateHover(null, span, doc);
        Assert.True(doc.NeedsLayout);
    }

    [Fact]
    public void Focus_NonFocusable_NotSet()
    {
        var div = new Element("div");
        InteractionStateHelper.SetFocus(div, null);
        Assert.False(div.IsFocused);
    }

    [Fact]
    public void IsFocusable_StandardElements_ReturnsTrue()
    {
        Assert.True(InteractionStateHelper.IsFocusable(new Element("a")));
        Assert.True(InteractionStateHelper.IsFocusable(new Element("input")));
        Assert.True(InteractionStateHelper.IsFocusable(new Element("button")));
        Assert.True(InteractionStateHelper.IsFocusable(new Element("textarea")));
        Assert.True(InteractionStateHelper.IsFocusable(new Element("select")));
    }

    [Fact]
    public void IsFocusable_TabIndex_ReturnsTrue()
    {
        var div = new Element("div", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tabindex"] = "0"
        });
        Assert.True(InteractionStateHelper.IsFocusable(div));
    }
}
