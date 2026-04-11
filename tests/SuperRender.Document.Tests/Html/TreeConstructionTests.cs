using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using Xunit;

namespace SuperRender.Document.Tests.Html;

public class TreeConstructionTests
{
    // ── Auto-close tests ────────────────────────────────────────────────────

    [Fact]
    public void ParagraphAutoClose_BlockInP_ClosesP()
    {
        // <p>hello<div>world</div> → p ends before div; they are siblings in body.
        var doc = new HtmlParser("<p>hello<div>world</div>").Parse();
        var body = doc.Body!;
        var elements = body.Children.OfType<Element>().ToList();

        Assert.Equal(2, elements.Count);
        Assert.Equal("p", elements[0].TagName);
        Assert.Equal("div", elements[1].TagName);

        // The p should contain "hello", the div should contain "world".
        Assert.Equal("hello", elements[0].InnerText);
        Assert.Equal("world", elements[1].InnerText);
    }

    [Fact]
    public void ParagraphAutoClose_PInP_ClosesFirst()
    {
        // <p>first<p>second → two sibling paragraphs, not nested.
        var doc = new HtmlParser("<p>first<p>second").Parse();
        var body = doc.Body!;
        var paragraphs = body.Children.OfType<Element>()
            .Where(e => e.TagName == "p").ToList();

        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("first", paragraphs[0].InnerText);
        Assert.Equal("second", paragraphs[1].InnerText);
    }

    [Fact]
    public void ListItemAutoClose_LiInLi_ClosesCurrent()
    {
        // <ul><li>a<li>b<li>c</ul> → three sibling li elements under ul.
        var doc = new HtmlParser("<ul><li>a<li>b<li>c</ul>").Parse();
        var body = doc.Body!;
        var ul = body.Children.OfType<Element>().First(e => e.TagName == "ul");
        var items = ul.Children.OfType<Element>().Where(e => e.TagName == "li").ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal("a", items[0].InnerText);
        Assert.Equal("b", items[1].InnerText);
        Assert.Equal("c", items[2].InnerText);
    }

    [Fact]
    public void DdDtAutoClose_DtAfterDd_ClosesDd()
    {
        // <dl><dd>a<dt>b</dl> → dd and dt are siblings under dl.
        var doc = new HtmlParser("<dl><dd>a<dt>b</dl>").Parse();
        var body = doc.Body!;
        var dl = body.Children.OfType<Element>().First(e => e.TagName == "dl");
        var children = dl.Children.OfType<Element>().ToList();

        Assert.Equal(2, children.Count);
        Assert.Equal("dd", children[0].TagName);
        Assert.Equal("dt", children[1].TagName);
        Assert.Equal("a", children[0].InnerText);
        Assert.Equal("b", children[1].InnerText);
    }

    [Fact]
    public void OptionAutoClose_OptionInOption()
    {
        // <select><option>a<option>b</select> → two sibling options under select.
        var doc = new HtmlParser("<select><option>a<option>b</select>").Parse();
        var body = doc.Body!;
        var select = body.Children.OfType<Element>().First(e => e.TagName == "select");
        var options = select.Children.OfType<Element>()
            .Where(e => e.TagName == "option").ToList();

        Assert.Equal(2, options.Count);
        Assert.Equal("a", options[0].InnerText);
        Assert.Equal("b", options[1].InnerText);
    }

    [Fact]
    public void HeadingAutoClose_H2InH1_ClosesH1()
    {
        // <h1>a<h2>b</h1> → h1 and h2 are siblings (h1 is auto-closed by h2).
        // The stray </h1> is ignored.
        var doc = new HtmlParser("<h1>a<h2>b</h1>").Parse();
        var body = doc.Body!;
        var headings = body.Children.OfType<Element>().ToList();

        Assert.Equal(2, headings.Count);
        Assert.Equal("h1", headings[0].TagName);
        Assert.Equal("h2", headings[1].TagName);
        Assert.Equal("a", headings[0].InnerText);
        Assert.Equal("b", headings[1].InnerText);
    }

    // ── Adoption agency tests ───────────────────────────────────────────────

    [Fact]
    public void AdoptionAgency_MisnestedBold_Corrected()
    {
        // <p><b>bold<i>both</b>italic</i></p>
        // The b should close before the i does. The original <i> (with "both")
        // stays inside <b>, and a new <i> is re-opened as a sibling of <b>
        // to receive "italic".
        var doc = new HtmlParser("<p><b>bold<i>both</b>italic</i></p>").Parse();
        var body = doc.Body!;
        var p = body.Children.OfType<Element>().First(e => e.TagName == "p");

        // p should have <b> and a re-opened <i> as direct children.
        var pChildren = p.Children.OfType<Element>().ToList();
        Assert.Equal(2, pChildren.Count);
        Assert.Equal("b", pChildren[0].TagName);
        Assert.Equal("i", pChildren[1].TagName);

        // The <b> should contain "bold" and the original <i> with "both".
        Assert.Contains("bold", pChildren[0].InnerText);
        Assert.Contains("both", pChildren[0].InnerText);

        // The re-opened <i> should contain "italic".
        Assert.Equal("italic", pChildren[1].InnerText);
    }

    // ── Nested list tests ───────────────────────────────────────────────────

    [Fact]
    public void AutoClose_NestedLists_WorkCorrectly()
    {
        // <ul><li>a<ul><li>nested</li></ul></li><li>b</li></ul>
        // The inner <ul> is a child of the first <li>, not auto-closed.
        var doc = new HtmlParser("<ul><li>a<ul><li>nested</li></ul></li><li>b</li></ul>").Parse();
        var body = doc.Body!;
        var outerUl = body.Children.OfType<Element>().First(e => e.TagName == "ul");
        var outerItems = outerUl.Children.OfType<Element>()
            .Where(e => e.TagName == "li").ToList();

        Assert.Equal(2, outerItems.Count);
        Assert.Equal("b", outerItems[1].InnerText);

        // The first li should contain a nested ul.
        var innerUl = outerItems[0].Children.OfType<Element>()
            .FirstOrDefault(e => e.TagName == "ul");
        Assert.NotNull(innerUl);

        var innerItems = innerUl!.Children.OfType<Element>()
            .Where(e => e.TagName == "li").ToList();
        Assert.Single(innerItems);
        Assert.Equal("nested", innerItems[0].InnerText);
    }

    [Fact]
    public void ParagraphAutoClose_WithText_PreservesText()
    {
        // Text content before and after auto-close is preserved correctly.
        // <p>before<div>middle</div>after
        var doc = new HtmlParser("<p>before<div>middle</div>after").Parse();
        var body = doc.Body!;
        var elements = body.Children.OfType<Element>().ToList();

        // p is auto-closed by div, so p and div are siblings.
        Assert.Equal("p", elements[0].TagName);
        Assert.Equal("div", elements[1].TagName);
        Assert.Equal("before", elements[0].InnerText);
        Assert.Equal("middle", elements[1].InnerText);

        // "after" should appear after the div (as a text node in body).
        var afterText = body.Children.OfType<TextNode>()
            .FirstOrDefault(t => t.Data.Contains("after"));
        Assert.NotNull(afterText);
    }

    [Fact]
    public void NoAutoClose_InlineInP_Allowed()
    {
        // <p><span>ok</span></p> — inline elements don't trigger auto-close.
        var doc = new HtmlParser("<p><span>ok</span></p>").Parse();
        var body = doc.Body!;
        var p = body.Children.OfType<Element>().First(e => e.TagName == "p");
        var span = p.Children.OfType<Element>().FirstOrDefault(e => e.TagName == "span");

        Assert.NotNull(span);
        Assert.Equal("ok", span!.InnerText);

        // span should be a CHILD of p, not a sibling.
        Assert.Same(p, span.Parent);
    }

    [Fact]
    public void FormattingElement_NotInStack_Ignored()
    {
        // End tag for formatting element not in stack is ignored.
        // <div>hello</b></div> — stray </b> should not corrupt the tree.
        var doc = new HtmlParser("<div>hello</b></div>").Parse();
        var body = doc.Body!;
        var div = body.Children.OfType<Element>().First(e => e.TagName == "div");

        Assert.Equal("hello", div.InnerText);
    }

    [Fact]
    public void AutoClose_DivInP_DivIsNotChildOfP()
    {
        // Verify div is a sibling of p, not a child of p.
        var doc = new HtmlParser("<p>text<div>block</div>").Parse();
        var body = doc.Body!;
        var p = body.Children.OfType<Element>().First(e => e.TagName == "p");
        var div = body.Children.OfType<Element>().First(e => e.TagName == "div");

        // Both p and div should be direct children of body.
        Assert.Same(body, p.Parent);
        Assert.Same(body, div.Parent);

        // div should NOT be a child of p.
        Assert.DoesNotContain(div, p.Children);
    }

    [Fact]
    public void ListItemAutoClose_NestedList_DoesNotCloseOuter()
    {
        // <ul><li>a<ol><li>b</ol></li></ul>
        // The inner <li> inside <ol> should NOT auto-close the outer <li>.
        var doc = new HtmlParser("<ul><li>a<ol><li>b</ol></li></ul>").Parse();
        var body = doc.Body!;
        var ul = body.Children.OfType<Element>().First(e => e.TagName == "ul");
        var outerItems = ul.Children.OfType<Element>()
            .Where(e => e.TagName == "li").ToList();

        // Only one outer <li> since the inner <li> is inside <ol>.
        Assert.Single(outerItems);

        // The outer <li> should contain the <ol>.
        var ol = outerItems[0].Children.OfType<Element>()
            .FirstOrDefault(e => e.TagName == "ol");
        Assert.NotNull(ol);

        // The <ol> should contain its own <li>.
        var innerItems = ol!.Children.OfType<Element>()
            .Where(e => e.TagName == "li").ToList();
        Assert.Single(innerItems);
        Assert.Equal("b", innerItems[0].InnerText);
    }

    [Fact]
    public void AdoptionAgency_SimpleCase_BoldEndTagMismatch()
    {
        // <div><b>text</div></b> → b is closed when div closes.
        // The </b> after </div> is a stray end tag and is ignored.
        var doc = new HtmlParser("<div><b>text</div></b>").Parse();
        var body = doc.Body!;
        var div = body.Children.OfType<Element>().First(e => e.TagName == "div");

        // <b> should be inside <div>.
        var b = div.Children.OfType<Element>().FirstOrDefault(e => e.TagName == "b");
        Assert.NotNull(b);
        Assert.Equal("text", b!.InnerText);
    }
}
