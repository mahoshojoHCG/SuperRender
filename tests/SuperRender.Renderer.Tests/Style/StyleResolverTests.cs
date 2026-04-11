using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Tests.Style;

public class StyleResolverTests
{
    private static (DomDocument doc, Element target) CreateDoc(string css, string bodyHtml)
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        // Parse CSS
        if (!string.IsNullOrEmpty(css))
        {
            var stylesheet = new CssParser(css).Parse();
            doc.Stylesheets.Add(stylesheet);
        }

        // Parse body content
        var parser = new SuperRender.Document.Html.HtmlParser(bodyHtml);
        var parsedDoc = parser.Parse();
        if (parsedDoc.Body != null)
        {
            foreach (var child in parsedDoc.Body.Children.ToList())
            {
                child.Parent?.RemoveChild(child);
                body.AppendChild(child);
            }
        }

        var target = body.Children.OfType<Element>().FirstOrDefault()!;
        return (doc, target);
    }

    [Fact]
    public void SingleRule_PropertyApplied()
    {
        var (doc, target) = CreateDoc("div { color: red; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("red"), style.Color);
    }

    [Fact]
    public void HigherSpecificity_Wins()
    {
        var (doc, _) = CreateDoc(
            ".cls { color: blue; } div { color: red; }",
            "<div class=\"cls\">test</div>");
        var target = doc.Body!.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("blue"), style.Color);
    }

    [Fact]
    public void LaterSourceOrder_Wins_SameSpecificity()
    {
        var (doc, _) = CreateDoc(
            "div { color: red; } div { color: blue; }",
            "<div>test</div>");
        var target = doc.Body!.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(Color.FromName("blue"), style.Color);
    }

    [Fact]
    public void InheritedProperty_FlowsToChild()
    {
        var (doc, _) = CreateDoc(
            "div { color: green; }",
            "<div><span>test</span></div>");
        var div = doc.Body!.Children.OfType<Element>().First();
        var span = div.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        Assert.Equal(Color.FromName("green"), styles[span].Color);
    }

    [Fact]
    public void NonInheritedProperty_DoesNotFlow()
    {
        var (doc, _) = CreateDoc(
            "div { background-color: red; }",
            "<div><span>test</span></div>");
        var div = doc.Body!.Children.OfType<Element>().First();
        var span = div.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        Assert.Equal(Color.Transparent, styles[span].BackgroundColor);
    }

    [Fact]
    public void DisplayNone_Applied()
    {
        var (doc, _) = CreateDoc(
            "div { display: none; }",
            "<div>test</div>");
        var target = doc.Body!.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(DisplayType.None, style.Display);
    }
}
