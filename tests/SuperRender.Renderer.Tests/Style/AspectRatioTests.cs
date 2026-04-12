using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class AspectRatioTests
{
    private static ComputedStyle ResolveStyle(string css, string bodyHtml)
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

        var target = body.Children.OfType<Element>().FirstOrDefault()!;
        var resolver = new StyleResolver(doc.Stylesheets);
        return resolver.Resolve(target);
    }

    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800)
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(html);

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        var engine = new LayoutEngine(measurer);
        return engine.BuildLayoutTree(doc, styles, viewportWidth, 600);
    }

    [Fact]
    public void AspectRatio_SingleNumber_Parsed()
    {
        var style = ResolveStyle("div { aspect-ratio: 2; }", "<div>test</div>");
        Assert.False(float.IsNaN(style.AspectRatio));
        Assert.Equal(2f, style.AspectRatio, 0.01f);
    }

    [Fact]
    public void AspectRatio_Fraction_Parsed()
    {
        var style = ResolveStyle("div { aspect-ratio: 16 / 9; }", "<div>test</div>");
        Assert.False(float.IsNaN(style.AspectRatio));
        Assert.Equal(16f / 9f, style.AspectRatio, 0.01f);
    }

    [Fact]
    public void AspectRatio_OneToOne_Parsed()
    {
        var style = ResolveStyle("div { aspect-ratio: 1 / 1; }", "<div>test</div>");
        Assert.False(float.IsNaN(style.AspectRatio));
        Assert.Equal(1f, style.AspectRatio, 0.01f);
    }

    [Fact]
    public void AspectRatio_Auto_IsNaN()
    {
        var style = ResolveStyle("div { aspect-ratio: auto; }", "<div>test</div>");
        Assert.True(float.IsNaN(style.AspectRatio));
    }

    [Fact]
    public void AspectRatio_None_IsNaN()
    {
        var style = ResolveStyle("div { aspect-ratio: none; }", "<div>test</div>");
        Assert.True(float.IsNaN(style.AspectRatio));
    }

    [Fact]
    public void AspectRatio_DefaultIsNaN()
    {
        var style = new ComputedStyle();
        Assert.True(float.IsNaN(style.AspectRatio));
    }

    [Fact]
    public void AspectRatio_4To3_Parsed()
    {
        var style = ResolveStyle("div { aspect-ratio: 4 / 3; }", "<div>test</div>");
        Assert.Equal(4f / 3f, style.AspectRatio, 0.01f);
    }

    [Fact]
    public void AspectRatio_Layout_HeightDerivedFromWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .box { width: 200px; aspect-ratio: 2 / 1; }
            </style></head><body>
                <div class='box'>content</div>
            </body></html>");

        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(200f, box!.Dimensions.Width, 1f);
        // aspect-ratio: 2/1 means width/height = 2, so height = 200/2 = 100
        Assert.Equal(100f, box.Dimensions.Height, 1f);
    }

    [Fact]
    public void AspectRatio_Layout_SquareBox()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .box { width: 150px; aspect-ratio: 1; }
            </style></head><body>
                <div class='box'>content</div>
            </body></html>");

        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(150f, box!.Dimensions.Width, 1f);
        Assert.Equal(150f, box.Dimensions.Height, 1f);
    }

    [Fact]
    public void AspectRatio_Layout_16by9()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .box { width: 320px; aspect-ratio: 16 / 9; }
            </style></head><body>
                <div class='box'>video</div>
            </body></html>");

        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(320f, box!.Dimensions.Width, 1f);
        // 320 / (16/9) = 320 * 9/16 = 180
        Assert.Equal(180f, box.Dimensions.Height, 1f);
    }

    [Fact]
    public void AspectRatio_Layout_ExplicitHeightOverrides()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .box { width: 200px; height: 50px; aspect-ratio: 1; }
            </style></head><body>
                <div class='box'>content</div>
            </body></html>");

        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        // Explicit height should override aspect-ratio
        Assert.Equal(200f, box!.Dimensions.Width, 1f);
        Assert.Equal(50f, box.Dimensions.Height, 1f);
    }

    [Fact]
    public void AspectRatio_Clone_Preserved()
    {
        var style = new ComputedStyle { AspectRatio = 1.5f };
        var clone = style.Clone();
        Assert.Equal(1.5f, clone.AspectRatio, 0.01f);
    }

    private static LayoutBox? FindBoxByClass(LayoutBox box, string className)
    {
        if (box.DomNode is Element e && e.ClassList.Contains(className))
            return box;
        foreach (var child in box.Children)
        {
            var found = FindBoxByClass(child, className);
            if (found != null) return found;
        }
        return null;
    }
}
