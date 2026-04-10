using SuperRender.Core;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Style;

public class OverflowTests
{
    [Fact]
    public void Overflow_Hidden_Parsed()
    {
        var (doc, target) = CreateDoc("div { overflow: hidden; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(OverflowType.Hidden, style.Overflow);
    }

    [Fact]
    public void Overflow_Scroll_Parsed()
    {
        var (doc, target) = CreateDoc("div { overflow: scroll; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(OverflowType.Scroll, style.Overflow);
    }

    [Fact]
    public void Overflow_Auto_Parsed()
    {
        var (doc, target) = CreateDoc("div { overflow: auto; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(OverflowType.Auto, style.Overflow);
    }

    [Fact]
    public void TextOverflow_Ellipsis_Parsed()
    {
        var (doc, target) = CreateDoc("div { text-overflow: ellipsis; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(TextOverflowType.Ellipsis, style.TextOverflow);
    }

    [Fact]
    public void Overflow_Hidden_EmitsClipCommands()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(@"<html><head><style>
            div { overflow: hidden; width: 100px; height: 50px; }
        </style></head><body><div>Some text content here</div></body></html>");

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var engine = new LayoutEngine(measurer);
        var root = engine.BuildLayoutTree(doc, styles, 800, 600);
        var paintList = Painter.Paint(root);

        // Should contain at least one ClipRectCommand and RestoreClipCommand
        Assert.Contains(paintList.Commands, c => c is ClipRectCommand);
        Assert.Contains(paintList.Commands, c => c is RestoreClipCommand);
    }

    [Fact]
    public void Overflow_Visible_NoClipCommands()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(@"<html><head><style>
            div { overflow: visible; width: 100px; height: 50px; }
        </style></head><body><div>text</div></body></html>");

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var engine = new LayoutEngine(measurer);
        var root = engine.BuildLayoutTree(doc, styles, 800, 600);
        var paintList = Painter.Paint(root);

        Assert.DoesNotContain(paintList.Commands, c => c is ClipRectCommand);
    }

    private static (Document doc, Element target) CreateDoc(string css, string bodyHtml)
    {
        var doc = new Document();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        if (!string.IsNullOrEmpty(css))
        {
            var stylesheet = new SuperRender.Core.Css.CssParser(css).Parse();
            doc.Stylesheets.Add(stylesheet);
        }

        var parser = new SuperRender.Core.Html.HtmlParser(bodyHtml);
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
}
