using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Tests;

/// <summary>
/// Shared test helper for creating DOM documents with CSS for style resolver tests.
/// </summary>
internal static class StyleTestHelper
{
    public static DomDocument CreateDoc(string css, string bodyHtml)
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        if (!string.IsNullOrEmpty(css))
        {
            var stylesheet = new CssParser(css).Parse();
            doc.Stylesheets.Add(stylesheet);
        }

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

        return doc;
    }

    public static Element GetFirstElement(DomDocument doc)
    {
        return doc.Body!.Children.OfType<Element>().First();
    }

    public static ComputedStyle ResolveFirst(string css, string bodyHtml)
    {
        var doc = CreateDoc(css, bodyHtml);
        var target = GetFirstElement(doc);
        var resolver = new StyleResolver(doc.Stylesheets);
        return resolver.Resolve(target);
    }
}
