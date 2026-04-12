using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class LogicalPropertyTests
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

    // ---------------------------------------------------------------
    // margin-block-start / margin-block-end
    // ---------------------------------------------------------------
    [Fact]
    public void MarginBlockStart_MapsToMarginTop()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-block-start: 20px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:1px"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(20, box!.Dimensions.Margin.Top, 0.1f);
    }

    [Fact]
    public void MarginBlockEnd_MapsToMarginBottom()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-block-end: 15px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(15, box!.Dimensions.Margin.Bottom, 0.1f);
    }

    // ---------------------------------------------------------------
    // margin-inline-start / margin-inline-end
    // ---------------------------------------------------------------
    [Fact]
    public void MarginInlineStart_MapsToMarginLeft()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-inline-start: 25px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(25, box!.Style.Margin.Left, 0.1f);
    }

    [Fact]
    public void MarginInlineEnd_MapsToMarginRight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-inline-end: 30px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(30, box!.Style.Margin.Right, 0.1f);
    }

    // ---------------------------------------------------------------
    // margin-block shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void MarginBlock_SingleValue_SetsTopAndBottom()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-block: 10px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:1px"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(10, box!.Dimensions.Margin.Top, 0.1f);
        Assert.Equal(10, box.Dimensions.Margin.Bottom, 0.1f);
    }

    [Fact]
    public void MarginBlock_TwoValues_SetsStartAndEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-block: 10px 20px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:1px"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(10, box!.Dimensions.Margin.Top, 0.1f);
        Assert.Equal(20, box.Dimensions.Margin.Bottom, 0.1f);
    }

    // ---------------------------------------------------------------
    // margin-inline shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void MarginInline_SingleValue_SetsLeftAndRight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-inline: 15px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(15, box!.Style.Margin.Left, 0.1f);
        Assert.Equal(15, box.Style.Margin.Right, 0.1f);
    }

    [Fact]
    public void MarginInline_TwoValues_SetsStartAndEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-inline: 10px 30px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(10, box!.Style.Margin.Left, 0.1f);
        Assert.Equal(30, box.Style.Margin.Right, 0.1f);
    }

    // ---------------------------------------------------------------
    // padding-block-start / padding-block-end
    // ---------------------------------------------------------------
    [Fact]
    public void PaddingBlockStart_MapsToPaddingTop()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-block-start: 12px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(12, box!.Style.Padding.Top, 0.1f);
    }

    [Fact]
    public void PaddingBlockEnd_MapsToPaddingBottom()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-block-end: 8px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(8, box!.Style.Padding.Bottom, 0.1f);
    }

    // ---------------------------------------------------------------
    // padding-inline-start / padding-inline-end
    // ---------------------------------------------------------------
    [Fact]
    public void PaddingInlineStart_MapsToPaddingLeft()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-inline-start: 5px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(5, box!.Style.Padding.Left, 0.1f);
    }

    [Fact]
    public void PaddingInlineEnd_MapsToPaddingRight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-inline-end: 7px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(7, box!.Style.Padding.Right, 0.1f);
    }

    // ---------------------------------------------------------------
    // padding-block / padding-inline shorthands
    // ---------------------------------------------------------------
    [Fact]
    public void PaddingBlock_SingleValue_SetsTopAndBottom()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-block: 6px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(6, box!.Style.Padding.Top, 0.1f);
        Assert.Equal(6, box.Style.Padding.Bottom, 0.1f);
    }

    [Fact]
    public void PaddingInline_TwoValues_SetsStartAndEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-inline: 4px 16px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(4, box!.Style.Padding.Left, 0.1f);
        Assert.Equal(16, box.Style.Padding.Right, 0.1f);
    }

    // ---------------------------------------------------------------
    // inline-size / block-size
    // ---------------------------------------------------------------
    [Fact]
    public void InlineSize_MapsToWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { inline-size: 200px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(200, box!.Dimensions.Width, 0.1f);
    }

    [Fact]
    public void BlockSize_MapsToHeight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { block-size: 150px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(150, box!.Dimensions.Height, 0.1f);
    }

    // ---------------------------------------------------------------
    // min/max inline-size / block-size
    // ---------------------------------------------------------------
    [Fact]
    public void MinInlineSize_MapsToMinWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { min-inline-size: 300px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(300, box!.Style.MinWidth, 0.1f);
    }

    [Fact]
    public void MaxInlineSize_MapsToMaxWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { max-inline-size: 500px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(500, box!.Style.MaxWidth, 0.1f);
    }

    [Fact]
    public void MinBlockSize_MapsToMinHeight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { min-block-size: 100px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(100, box!.Style.MinHeight, 0.1f);
    }

    [Fact]
    public void MaxBlockSize_MapsToMaxHeight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { max-block-size: 400px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(400, box!.Style.MaxHeight, 0.1f);
    }

    [Fact]
    public void MaxBlockSize_None_MapsToInfinity()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { max-block-size: none; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.True(float.IsPositiveInfinity(box!.Style.MaxHeight));
    }

    // ---------------------------------------------------------------
    // text-align: start / end
    // ---------------------------------------------------------------
    [Fact]
    public void TextAlign_Start_MapsToLeft()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { text-align: start; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(TextAlign.Left, box!.Style.TextAlign);
    }

    [Fact]
    public void TextAlign_End_MapsToRight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { text-align: end; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(TextAlign.Right, box!.Style.TextAlign);
    }

    // ---------------------------------------------------------------
    // Combined logical properties
    // ---------------------------------------------------------------
    [Fact]
    public void InlineSize_And_BlockSize_Together()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { inline-size: 250px; block-size: 120px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(250, box!.Dimensions.Width, 0.1f);
        Assert.Equal(120, box.Dimensions.Height, 0.1f);
    }

    [Fact]
    public void PaddingBlock_TwoValues_SetsStartAndEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-block: 8px 16px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(8, box!.Style.Padding.Top, 0.1f);
        Assert.Equal(16, box.Style.Padding.Bottom, 0.1f);
    }

    [Fact]
    public void PaddingInline_SingleValue_SetsBoth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { padding-inline: 12px; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.Equal(12, box!.Style.Padding.Left, 0.1f);
        Assert.Equal(12, box.Style.Padding.Right, 0.1f);
    }

    [Fact]
    public void MaxInlineSize_None_MapsToInfinity()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { max-inline-size: none; width: 100px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        Assert.True(float.IsPositiveInfinity(box!.Style.MaxWidth));
    }

    [Fact]
    public void MinBlockSize_ZeroByDefault()
    {
        var style = new ComputedStyle();
        Assert.Equal(0, style.MinHeight);
    }

    [Fact]
    public void MarginInline_AffectsLayoutPosition()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .box { margin-inline-start: 50px; width: 100px; height: 50px; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""box"">text</div>
            </body></html>");
        var box = FindBoxByClass(root, "box");
        Assert.NotNull(box);
        // The box's X position should reflect the left margin
        Assert.True(box!.Dimensions.X >= 50,
            "Box should be offset by margin-inline-start");
    }
}
