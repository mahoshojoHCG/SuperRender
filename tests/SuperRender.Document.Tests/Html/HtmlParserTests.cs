using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using Xunit;

namespace SuperRender.Document.Tests.Html;

public class HtmlParserTests
{
    [Fact]
    public void ParseSimplePage_CorrectTreeStructure()
    {
        var doc = new HtmlParser("<html><body><p>hello</p></body></html>").Parse();
        Assert.NotNull(doc.DocumentElement);
        Assert.Equal("html", doc.DocumentElement!.TagName);
        Assert.NotNull(doc.Body);
        Assert.Single(doc.Body!.Children.OfType<Element>());
        var p = doc.Body.Children.OfType<Element>().First();
        Assert.Equal("p", p.TagName);
    }

    [Fact]
    public void BareP_AutoInsertsHtmlHeadBody()
    {
        var doc = new HtmlParser("<p>hello</p>").Parse();
        Assert.NotNull(doc.DocumentElement);
        Assert.Equal("html", doc.DocumentElement!.TagName);
        Assert.NotNull(doc.Body);
    }

    [Fact]
    public void MultipleChildren()
    {
        var doc = new HtmlParser("<div><p>a</p><p>b</p></div>").Parse();
        var body = doc.Body!;
        var div = body.Children.OfType<Element>().First(e => e.TagName == "div");
        Assert.Equal(2, div.Children.OfType<Element>().Count());
    }

    [Fact]
    public void VoidElement_NoClosingTag()
    {
        var doc = new HtmlParser("<p>before<br>after</p>").Parse();
        var body = doc.Body!;
        var p = body.Children.OfType<Element>().First(e => e.TagName == "p");
        Assert.Contains(p.Children, c => c is Element e && e.TagName == "br");
    }

    [Fact]
    public void StyleTag_ContentsPreserved()
    {
        var doc = new HtmlParser("<html><head><style>div { color: red; }</style></head><body></body></html>").Parse();
        var head = doc.Head;
        Assert.NotNull(head);
        var style = head!.Children.OfType<Element>().FirstOrDefault(e => e.TagName == "style");
        Assert.NotNull(style);
        Assert.Contains("color: red", style!.InnerText);
    }

    [Fact]
    public void AttributesParsed()
    {
        var doc = new HtmlParser("<div id=\"main\" class=\"container big\"></div>").Parse();
        var div = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal("main", div.Id);
        Assert.Contains("container", div.ClassList);
        Assert.Contains("big", div.ClassList);
    }
}
