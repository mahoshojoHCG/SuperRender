using SuperRender.Core.Dom;
using Xunit;

namespace SuperRender.Tests.Dom;

public class DomMutationTests
{
    [Fact]
    public void AppendChild_UpdatesParent()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);
        Assert.Equal(doc, div.Parent);
        Assert.Contains(div, doc.Children);
    }

    [Fact]
    public void RemoveChild_ClearsParent()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);
        doc.RemoveChild(div);
        Assert.Null(div.Parent);
        Assert.DoesNotContain(div, doc.Children);
    }

    [Fact]
    public void InsertBefore_CorrectPosition()
    {
        var doc = new Document();
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        doc.AppendChild(a);
        doc.AppendChild(c);
        doc.InsertBefore(b, c);
        Assert.Equal(3, doc.Children.Count);
        Assert.Equal(b, doc.Children[1]);
    }

    [Fact]
    public void SetAttribute_TriggersDirty()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);
        doc.NeedsLayout = false;
        div.SetAttribute("class", "test");
        Assert.True(doc.NeedsLayout);
    }

    [Fact]
    public void TextNode_DataChange_TriggersDirty()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        var text = doc.CreateTextNode("hello");
        doc.AppendChild(div);
        div.AppendChild(text);
        doc.NeedsLayout = false;
        text.Data = "world";
        Assert.True(doc.NeedsLayout);
    }

    [Fact]
    public void SetTextContent_ReplacesChildren()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        var span = doc.CreateElement("span");
        doc.AppendChild(div);
        div.AppendChild(span);
        DomMutationApi.SetTextContent(div, "new text");
        Assert.Single(div.Children);
        Assert.IsType<TextNode>(div.Children[0]);
        Assert.Equal("new text", ((TextNode)div.Children[0]).Data);
    }

    [Fact]
    public void AddClass_AddsToElement()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        DomMutationApi.AddClass(div, "foo");
        Assert.Contains("foo", div.ClassList);
    }

    [Fact]
    public void RemoveClass_RemovesFromElement()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        div.SetAttribute("class", "foo bar");
        DomMutationApi.RemoveClass(div, "foo");
        Assert.DoesNotContain("foo", div.ClassList);
        Assert.Contains("bar", div.ClassList);
    }

    [Fact]
    public void ToggleClass_TogglesPresence()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        DomMutationApi.ToggleClass(div, "active");
        Assert.Contains("active", div.ClassList);
        DomMutationApi.ToggleClass(div, "active");
        Assert.DoesNotContain("active", div.ClassList);
    }

    [Fact]
    public void CloneElement_DeepClone()
    {
        var doc = new Document();
        var div = doc.CreateElement("div");
        div.SetAttribute("id", "original");
        var span = doc.CreateElement("span");
        div.AppendChild(span);
        var text = doc.CreateTextNode("hello");
        span.AppendChild(text);

        var clone = DomMutationApi.CloneElement(div, deep: true);
        Assert.Equal("original", clone.Id);
        Assert.Single(clone.Children);
        var clonedSpan = clone.Children[0] as Element;
        Assert.NotNull(clonedSpan);
        Assert.Equal("span", clonedSpan!.TagName);
        Assert.Single(clonedSpan.Children);
        Assert.IsType<TextNode>(clonedSpan.Children[0]);
    }

    [Fact]
    public void SiblingLinks_Maintained()
    {
        var doc = new Document();
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        doc.AppendChild(a);
        doc.AppendChild(b);
        doc.AppendChild(c);

        Assert.Null(a.PreviousSibling);
        Assert.Equal(b, a.NextSibling);
        Assert.Equal(a, b.PreviousSibling);
        Assert.Equal(c, b.NextSibling);
        Assert.Equal(b, c.PreviousSibling);
        Assert.Null(c.NextSibling);
    }
}
