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
    public void ContentBox_WidthDoesNotIncludePadding()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { width: 200px; padding: 20px; box-sizing: content-box; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        Assert.Equal(200, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void BorderBox_WidthIncludesPaddingAndBorder()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { width: 200px; padding: 20px; border-width: 5px; border-style: solid; box-sizing: border-box; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        // contentWidth = 200 - 40(padding) - 10(border) = 150
        Assert.Equal(150, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void BorderBox_HeightIncludesPaddingAndBorder()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { height: 100px; padding: 10px; border-width: 5px; border-style: solid; box-sizing: border-box; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        // contentHeight = 100 - 20(padding) - 10(border) = 70
        Assert.Equal(70, div!.Dimensions.Height, 0.1f);
    }

    [Fact]
    public void MinWidth_ClampsSmallContent()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { width: 50px; min-width: 100px; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        Assert.Equal(100, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void MaxWidth_ClampsLargeContent()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { width: 500px; max-width: 300px; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        Assert.Equal(300, div!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void MinHeight_Applied()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { min-height: 200px; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        Assert.True(div!.Dimensions.Height >= 200);
    }

    [Fact]
    public void MaxHeight_Clamps()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { height: 500px; max-height: 100px; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        Assert.Equal(100, div!.Dimensions.Height, 0.1f);
    }

    [Fact]
    public void BorderBox_MinWidth_AdjustsForPadding()
    {
        var root = LayoutHtml(@"<html><head><style>
            div { width: 50px; min-width: 200px; padding: 20px; box-sizing: border-box; }
        </style></head><body><div>test</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);
        // min-width in border-box: content = 200 - 40 = 160
        Assert.Equal(160, div!.Dimensions.Width, 0.1f);
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
