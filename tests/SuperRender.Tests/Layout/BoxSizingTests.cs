using SuperRender.Core;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Layout;

public class BoxSizingTests
{
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
    public void BorderBox_SpecifiedWidth_IncludesPaddingAndBorder()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { box-sizing: border-box; width: 200px; padding: 20px; border-width: 5px; border-style: solid; }
            </style></head><body><div>text</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        // border-box: content width = 200 - 20*2 - 5*2 = 150
        Assert.Equal(150f, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void ContentBox_WidthIsContentOnly()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { box-sizing: content-box; width: 200px; padding: 20px; border-width: 5px; border-style: solid; }
            </style></head><body><div>text</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        // content-box: content width stays at 200
        Assert.Equal(200f, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void MinWidth_PreventsShrinkingBelowMinimum()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { width: 50px; min-width: 100px; }
            </style></head><body><div>text</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        Assert.True(div!.Dimensions.Width >= 100f);
    }

    [Fact]
    public void MaxWidth_PreventsGrowingBeyondMaximum()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { width: 500px; max-width: 300px; }
            </style></head><body><div>text</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        Assert.True(div!.Dimensions.Width <= 300f);
    }

    [Fact]
    public void MinHeight_PreventsShrinking()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { min-height: 200px; }
            </style></head><body><div>x</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        Assert.True(div!.Dimensions.Height >= 200f);
    }

    [Fact]
    public void MaxHeight_PreventsGrowing()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { height: 500px; max-height: 300px; }
            </style></head><body><div>x</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        Assert.True(div!.Dimensions.Height <= 300f);
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
}
