using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Dom;
using Xunit;

namespace SuperRender.Document.Tests.Dom;

public class DomMethodTests
{
    // =====================================================================
    // ReplaceChild
    // =====================================================================

    [Fact]
    public void ReplaceChild_Basic_ReplacesOldWithNew()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var oldChild = doc.CreateElement("span");
        var newChild = doc.CreateElement("p");
        doc.AppendChild(parent);
        parent.AppendChild(oldChild);

        var returned = parent.ReplaceChild(newChild, oldChild);

        Assert.Same(oldChild, returned);
        Assert.DoesNotContain(oldChild, parent.Children);
        Assert.Contains(newChild, parent.Children);
        Assert.Equal(parent, newChild.Parent);
        Assert.Null(oldChild.Parent);
    }

    [Fact]
    public void ReplaceChild_MaintainsSiblingLinks()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        var replacement = doc.CreateElement("x");
        doc.AppendChild(parent);
        parent.AppendChild(a);
        parent.AppendChild(b);
        parent.AppendChild(c);

        parent.ReplaceChild(replacement, b);

        Assert.Equal(a, replacement.PreviousSibling);
        Assert.Equal(c, replacement.NextSibling);
        Assert.Equal(replacement, a.NextSibling);
        Assert.Equal(replacement, c.PreviousSibling);
    }

    [Fact]
    public void ReplaceChild_InvalidOldChild_Throws()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var stranger = doc.CreateElement("span");
        var newChild = doc.CreateElement("p");
        doc.AppendChild(parent);

        Assert.Throws<InvalidOperationException>(() => parent.ReplaceChild(newChild, stranger));
    }

    [Fact]
    public void ReplaceChild_NullNewChild_Throws()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var oldChild = doc.CreateElement("span");
        doc.AppendChild(parent);
        parent.AppendChild(oldChild);

        Assert.Throws<ArgumentNullException>(() => parent.ReplaceChild(null!, oldChild));
    }

    [Fact]
    public void ReplaceChild_NullOldChild_Throws()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var newChild = doc.CreateElement("span");
        doc.AppendChild(parent);

        Assert.Throws<ArgumentNullException>(() => parent.ReplaceChild(newChild, null!));
    }

    // =====================================================================
    // CloneNode
    // =====================================================================

    [Fact]
    public void CloneNode_ShallowElement_CopiesAttrsNotChildren()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("id", "test");
        div.SetAttribute("class", "foo bar");
        var child = doc.CreateElement("span");
        div.AppendChild(child);

        var clone = div.CloneNode(false) as Element;

        Assert.NotNull(clone);
        Assert.Equal("div", clone!.TagName);
        Assert.Equal("test", clone.Id);
        Assert.Contains("foo", clone.ClassList);
        Assert.Empty(clone.Children);
        Assert.NotSame(div, clone);
    }

    [Fact]
    public void CloneNode_DeepElement_CopiesChildren()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("id", "parent");
        var span = doc.CreateElement("span");
        var text = doc.CreateTextNode("hello");
        span.AppendChild(text);
        div.AppendChild(span);

        var clone = div.CloneNode(true) as Element;

        Assert.NotNull(clone);
        Assert.Equal("parent", clone!.Id);
        Assert.Single(clone.Children);

        var clonedSpan = clone.Children[0] as Element;
        Assert.NotNull(clonedSpan);
        Assert.Equal("span", clonedSpan!.TagName);
        Assert.NotSame(span, clonedSpan);

        var clonedText = clonedSpan.Children[0] as TextNode;
        Assert.NotNull(clonedText);
        Assert.Equal("hello", clonedText!.Data);
        Assert.NotSame(text, clonedText);
    }

    [Fact]
    public void CloneNode_TextNode_CopiesData()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("test data");

        var clone = text.CloneNode(false) as TextNode;

        Assert.NotNull(clone);
        Assert.Equal("test data", clone!.Data);
        Assert.NotSame(text, clone);
        Assert.Equal(doc, clone.OwnerDocument);
    }

    [Fact]
    public void CloneNode_DoesNotCloneParent()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var child = doc.CreateElement("span");
        parent.AppendChild(child);

        var clone = child.CloneNode(false);

        Assert.Null(clone.Parent);
    }

    // =====================================================================
    // Contains
    // =====================================================================

    [Fact]
    public void Contains_Self_ReturnsTrue()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        Assert.True(div.Contains(div));
    }

    [Fact]
    public void Contains_DirectChild_ReturnsTrue()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var child = doc.CreateElement("span");
        parent.AppendChild(child);

        Assert.True(parent.Contains(child));
    }

    [Fact]
    public void Contains_DeepDescendant_ReturnsTrue()
    {
        var doc = new DomDocument();
        var grandparent = doc.CreateElement("div");
        var parent = doc.CreateElement("p");
        var child = doc.CreateElement("span");
        grandparent.AppendChild(parent);
        parent.AppendChild(child);

        Assert.True(grandparent.Contains(child));
    }

    [Fact]
    public void Contains_Unrelated_ReturnsFalse()
    {
        var doc = new DomDocument();
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");

        Assert.False(a.Contains(b));
    }

    [Fact]
    public void Contains_ChildDoesNotContainParent()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var child = doc.CreateElement("span");
        parent.AppendChild(child);

        Assert.False(child.Contains(parent));
    }

    [Fact]
    public void Contains_Null_ReturnsFalse()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        Assert.False(div.Contains(null));
    }

    // =====================================================================
    // TextContent
    // =====================================================================

    [Fact]
    public void TextContent_Get_CollectsAllTextNodes()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        var text1 = doc.CreateTextNode("Hello ");
        var span = doc.CreateElement("span");
        var text2 = doc.CreateTextNode("World");
        div.AppendChild(text1);
        div.AppendChild(span);
        span.AppendChild(text2);

        Assert.Equal("Hello World", div.TextContent);
    }

    [Fact]
    public void TextContent_Set_ReplacesAllChildren()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        var span = doc.CreateElement("span");
        var text = doc.CreateTextNode("old");
        div.AppendChild(span);
        span.AppendChild(text);

        div.TextContent = "new content";

        Assert.Single(div.Children);
        var textNode = Assert.IsType<TextNode>(div.Children[0]);
        Assert.Equal("new content", textNode.Data);
    }

    [Fact]
    public void TextContent_SetEmpty_ClearsChildren()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.AppendChild(doc.CreateTextNode("some text"));

        div.TextContent = "";

        Assert.Empty(div.Children);
    }

    [Fact]
    public void TextContent_TextNode_ReturnsData()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("hello");

        Assert.Equal("hello", text.TextContent);
    }

    [Fact]
    public void TextContent_TextNode_SetUpdatesData()
    {
        var doc = new DomDocument();
        var text = doc.CreateTextNode("old");

        text.TextContent = "new";

        Assert.Equal("new", text.Data);
    }

    // =====================================================================
    // HasChildNodes
    // =====================================================================

    [Fact]
    public void HasChildNodes_WithChildren_ReturnsTrue()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.AppendChild(doc.CreateTextNode("text"));

        Assert.True(div.HasChildNodes());
    }

    [Fact]
    public void HasChildNodes_Empty_ReturnsFalse()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        Assert.False(div.HasChildNodes());
    }

    // =====================================================================
    // Matches
    // =====================================================================

    [Fact]
    public void Matches_MatchingTagSelector_ReturnsTrue()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);

        Assert.True(div.Matches("div"));
    }

    [Fact]
    public void Matches_MatchingClassSelector_ReturnsTrue()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("class", "active");
        doc.AppendChild(div);

        Assert.True(div.Matches(".active"));
    }

    [Fact]
    public void Matches_NonMatchingSelector_ReturnsFalse()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);

        Assert.False(div.Matches("span"));
    }

    [Fact]
    public void Matches_IdSelector_Works()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("id", "main");
        doc.AppendChild(div);

        Assert.True(div.Matches("#main"));
        Assert.False(div.Matches("#other"));
    }

    // =====================================================================
    // Closest
    // =====================================================================

    [Fact]
    public void Closest_MatchesSelf_ReturnsSelf()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("class", "target");
        doc.AppendChild(div);

        Assert.Same(div, div.Closest(".target"));
    }

    [Fact]
    public void Closest_FindsAncestor_ReturnsAncestor()
    {
        var doc = new DomDocument();
        var outer = doc.CreateElement("section");
        outer.SetAttribute("class", "container");
        var inner = doc.CreateElement("div");
        var span = doc.CreateElement("span");
        doc.AppendChild(outer);
        outer.AppendChild(inner);
        inner.AppendChild(span);

        Assert.Same(outer, span.Closest(".container"));
    }

    [Fact]
    public void Closest_NoMatch_ReturnsNull()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        doc.AppendChild(div);

        Assert.Null(div.Closest(".nonexistent"));
    }

    // =====================================================================
    // Dataset
    // =====================================================================

    [Fact]
    public void Dataset_DataAttributes_ReturnsMappedDictionary()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("data-name", "Alice");
        div.SetAttribute("data-age", "30");
        div.SetAttribute("class", "test");  // not a data attribute

        var ds = div.Dataset;

        Assert.Equal(2, ds.Count);
        Assert.Equal("Alice", ds["name"]);
        Assert.Equal("30", ds["age"]);
    }

    [Fact]
    public void Dataset_KebabCase_ConvertsToCamelCase()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("data-user-name", "Bob");
        div.SetAttribute("data-last-login-date", "2024-01-01");

        var ds = div.Dataset;

        Assert.Equal("Bob", ds["userName"]);
        Assert.Equal("2024-01-01", ds["lastLoginDate"]);
    }

    [Fact]
    public void Dataset_NoDataAttributes_ReturnsEmpty()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("id", "test");

        Assert.Empty(div.Dataset);
    }

    // =====================================================================
    // HasAttribute
    // =====================================================================

    [Fact]
    public void HasAttribute_Present_ReturnsTrue()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("id", "test");

        Assert.True(div.HasAttribute("id"));
    }

    [Fact]
    public void HasAttribute_CaseInsensitive_ReturnsTrue()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("Id", "test");

        Assert.True(div.HasAttribute("id"));
        Assert.True(div.HasAttribute("ID"));
    }

    [Fact]
    public void HasAttribute_Absent_ReturnsFalse()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        Assert.False(div.HasAttribute("id"));
    }

    // =====================================================================
    // ToggleAttribute
    // =====================================================================

    [Fact]
    public void ToggleAttribute_NotPresent_Adds()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        bool result = div.ToggleAttribute("hidden");

        Assert.True(result);
        Assert.True(div.HasAttribute("hidden"));
    }

    [Fact]
    public void ToggleAttribute_Present_Removes()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("hidden", "");

        bool result = div.ToggleAttribute("hidden");

        Assert.False(result);
        Assert.False(div.HasAttribute("hidden"));
    }

    [Fact]
    public void ToggleAttribute_ForceTrue_EnsuresPresent()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");

        bool result = div.ToggleAttribute("hidden", true);

        Assert.True(result);
        Assert.True(div.HasAttribute("hidden"));
    }

    [Fact]
    public void ToggleAttribute_ForceFalse_EnsuresRemoved()
    {
        var doc = new DomDocument();
        var div = doc.CreateElement("div");
        div.SetAttribute("hidden", "");

        bool result = div.ToggleAttribute("hidden", false);

        Assert.False(result);
        Assert.False(div.HasAttribute("hidden"));
    }

    // =====================================================================
    // FirstElementChild / LastElementChild / ChildElementCount
    // =====================================================================

    [Fact]
    public void FirstElementChild_MixedChildren_ReturnsFirstElement()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        parent.AppendChild(doc.CreateTextNode("text"));
        var span = doc.CreateElement("span");
        parent.AppendChild(span);
        parent.AppendChild(doc.CreateElement("p"));

        Assert.Same(span, parent.FirstElementChild);
    }

    [Fact]
    public void FirstElementChild_NoElements_ReturnsNull()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        parent.AppendChild(doc.CreateTextNode("only text"));

        Assert.Null(parent.FirstElementChild);
    }

    [Fact]
    public void LastElementChild_MixedChildren_ReturnsLastElement()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        parent.AppendChild(doc.CreateElement("span"));
        var p = doc.CreateElement("p");
        parent.AppendChild(p);
        parent.AppendChild(doc.CreateTextNode("text"));

        Assert.Same(p, parent.LastElementChild);
    }

    [Fact]
    public void LastElementChild_NoElements_ReturnsNull()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        parent.AppendChild(doc.CreateTextNode("only text"));

        Assert.Null(parent.LastElementChild);
    }

    [Fact]
    public void ChildElementCount_MixedChildren_CountsOnlyElements()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        parent.AppendChild(doc.CreateTextNode("text"));
        parent.AppendChild(doc.CreateElement("span"));
        parent.AppendChild(doc.CreateTextNode("more text"));
        parent.AppendChild(doc.CreateElement("p"));

        Assert.Equal(2, parent.ChildElementCount);
    }

    [Fact]
    public void ChildElementCount_NoChildren_ReturnsZero()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");

        Assert.Equal(0, parent.ChildElementCount);
    }

    // =====================================================================
    // After
    // =====================================================================

    [Fact]
    public void After_InsertsNodesAfterElement()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        var d = doc.CreateElement("d");
        doc.AppendChild(parent);
        parent.AppendChild(a);
        parent.AppendChild(d);

        a.After(b, c);

        Assert.Equal(4, parent.Children.Count);
        Assert.Same(a, parent.Children[0]);
        Assert.Same(b, parent.Children[1]);
        Assert.Same(c, parent.Children[2]);
        Assert.Same(d, parent.Children[3]);
    }

    [Fact]
    public void After_NoParent_DoesNothing()
    {
        var doc = new DomDocument();
        var orphan = doc.CreateElement("div");
        var node = doc.CreateElement("span");

        orphan.After(node);  // Should not throw

        Assert.Null(node.Parent);
    }

    // =====================================================================
    // Before
    // =====================================================================

    [Fact]
    public void Before_InsertsNodesBeforeElement()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        var d = doc.CreateElement("d");
        doc.AppendChild(parent);
        parent.AppendChild(a);
        parent.AppendChild(d);

        d.Before(b, c);

        Assert.Equal(4, parent.Children.Count);
        Assert.Same(a, parent.Children[0]);
        Assert.Same(b, parent.Children[1]);
        Assert.Same(c, parent.Children[2]);
        Assert.Same(d, parent.Children[3]);
    }

    [Fact]
    public void Before_NoParent_DoesNothing()
    {
        var doc = new DomDocument();
        var orphan = doc.CreateElement("div");
        var node = doc.CreateElement("span");

        orphan.Before(node);  // Should not throw

        Assert.Null(node.Parent);
    }

    // =====================================================================
    // Remove
    // =====================================================================

    [Fact]
    public void Remove_RemovesFromParent()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var child = doc.CreateElement("span");
        doc.AppendChild(parent);
        parent.AppendChild(child);

        child.Remove();

        Assert.Empty(parent.Children);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void Remove_NoParent_DoesNothing()
    {
        var doc = new DomDocument();
        var orphan = doc.CreateElement("div");

        orphan.Remove();  // Should not throw

        Assert.Null(orphan.Parent);
    }

    [Fact]
    public void Remove_MaintainsSiblingLinks()
    {
        var doc = new DomDocument();
        var parent = doc.CreateElement("div");
        var a = doc.CreateElement("a");
        var b = doc.CreateElement("b");
        var c = doc.CreateElement("c");
        doc.AppendChild(parent);
        parent.AppendChild(a);
        parent.AppendChild(b);
        parent.AppendChild(c);

        b.Remove();

        Assert.Equal(2, parent.Children.Count);
        Assert.Equal(c, a.NextSibling);
        Assert.Equal(a, c.PreviousSibling);
    }
}
