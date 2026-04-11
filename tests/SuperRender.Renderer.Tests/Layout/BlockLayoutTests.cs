using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class BlockLayoutTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800)
    {
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer());
        var doc = pipeline.LoadHtml(html);

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        var engine = new LayoutEngine(new MonospaceTextMeasurer());
        return engine.BuildLayoutTree(doc, styles, viewportWidth, 600);
    }

    [Fact]
    public void ExplicitWidth_Respected()
    {
        var root = LayoutHtml("<html><head><style>div { width: 200px; }</style></head><body><div>text</div></body></html>");
        // Find the div layout box
        var divBox = FindBox(root, "div");
        Assert.NotNull(divBox);
        Assert.Equal(200, divBox!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void AutoWidth_FillsContainer()
    {
        var root = LayoutHtml("<html><head><style></style></head><body><div>text</div></body></html>", 800);
        var divBox = FindBox(root, "div");
        Assert.NotNull(divBox);
        // Should fill available width (minus any margins/padding from body)
        Assert.True(divBox!.Dimensions.Width > 0);
    }

    [Fact]
    public void ExplicitHeight_Respected()
    {
        var root = LayoutHtml("<html><head><style>div { height: 100px; }</style></head><body><div>text</div></body></html>");
        var divBox = FindBox(root, "div");
        Assert.NotNull(divBox);
        Assert.Equal(100, divBox!.Dimensions.Height, 0.1f);
    }

    [Fact]
    public void NestedDivs_InnerConstrained()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .outer { width: 400px; padding: 10px; }
                .inner { width: 200px; }
            </style></head><body>
                <div class=""outer""><div class=""inner"">text</div></div>
            </body></html>", 800);

        var inner = FindBoxByClass(root, "inner");
        Assert.NotNull(inner);
        Assert.Equal(200, inner!.Dimensions.Width, 0.1f);
    }

    private static LayoutBox? FindBox(LayoutBox box, string tagName)
    {
        if (box.DomNode is Element e && e.TagName == tagName)
            return box;
        foreach (var child in box.Children)
        {
            var found = FindBox(child, tagName);
            if (found != null) return found;
        }
        return null;
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
