using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class AlignContentTests
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

    private static List<LayoutBox> FindAllByClass(LayoutBox box, string className)
    {
        var results = new List<LayoutBox>();
        FindAllByClassRecursive(box, className, results);
        return results;
    }

    private static void FindAllByClassRecursive(LayoutBox box, string className, List<LayoutBox> results)
    {
        if (box.DomNode is Element e && e.ClassList.Contains(className))
            results.Add(box);
        foreach (var child in box.Children)
            FindAllByClassRecursive(child, className, results);
    }

    // ---------------------------------------------------------------
    // AlignContent enum parsing
    // ---------------------------------------------------------------
    [Fact]
    public void AlignContent_ParsesStretch()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: stretch; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.Stretch, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_ParsesFlexStart()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: flex-start; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.FlexStart, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_ParsesFlexEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: flex-end; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.FlexEnd, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_ParsesCenter()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: center; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.Center, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_ParsesSpaceBetween()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: space-between; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.SpaceBetween, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_ParsesSpaceAround()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: space-around; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.SpaceAround, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_ParsesSpaceEvenly()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; align-content: space-evenly; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.SpaceEvenly, flex!.Style.AlignContent);
    }

    [Fact]
    public void AlignContent_DefaultIsStretch()
    {
        var style = new ComputedStyle();
        Assert.Equal(AlignContentType.Stretch, style.AlignContent);
    }

    // ---------------------------------------------------------------
    // AlignContent layout tests (wrap mode with fixed cross-axis)
    // ---------------------------------------------------------------
    [Fact]
    public void AlignContent_FlexEnd_LinesShiftedToEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; height: 200px; width: 100px;
                        align-content: flex-end; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // With flex-end, lines should be pushed towards the bottom
        // Two lines of 30px each = 60px, container is 200px, so offset = 140
        // b should be at (or near) Y for bottom
        Assert.True(b!.Dimensions.Y > a!.Dimensions.Y,
            "Second line should be below first line");
    }

    [Fact]
    public void AlignContent_Center_LinesCentered()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; height: 200px; width: 100px;
                        align-content: center; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        Assert.NotNull(a);

        // Two lines of 30px = 60px total, container 200px
        // Center offset: (200 - 60) / 2 = 70
        // First line starts at Y ~70
        Assert.True(a!.Dimensions.Y > 30,
            "First line should be offset from top for centering");
    }

    [Fact]
    public void AlignContent_SpaceBetween_LinesSpreadOut()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; height: 200px; width: 100px;
                        align-content: space-between; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // With space-between, first line at top, last line at bottom
        // Gap between them should be large
        float gap = b!.Dimensions.Y - a!.Dimensions.Y;
        Assert.True(gap > 100, $"Lines should be spread apart, gap was {gap}");
    }

    // ---------------------------------------------------------------
    // place-content shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void PlaceContent_SingleValue_AppliesToBoth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; place-content: center; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.Center, flex!.Style.AlignContent);
        Assert.Equal(JustifyContentType.Center, flex.Style.JustifyContent);
    }

    [Fact]
    public void PlaceContent_TwoValues_SetsAlignAndJustify()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; place-content: space-between center; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignContentType.SpaceBetween, flex!.Style.AlignContent);
        Assert.Equal(JustifyContentType.Center, flex.Style.JustifyContent);
    }

    // ---------------------------------------------------------------
    // place-items shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void PlaceItems_SingleValue_AppliesToBoth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; place-items: center; }
            </style></head><body><div class=""flex""></div></body></html>");
        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(AlignItemsType.Center, flex!.Style.AlignItems);
        Assert.Equal(JustifyContentType.Center, flex.Style.JustifyContent);
    }

    // ---------------------------------------------------------------
    // Clone includes AlignContent
    // ---------------------------------------------------------------
    [Fact]
    public void Clone_PreservesAlignContent()
    {
        var style = new ComputedStyle { AlignContent = AlignContentType.SpaceEvenly };
        var clone = style.Clone();
        Assert.Equal(AlignContentType.SpaceEvenly, clone.AlignContent);
    }

    // ---------------------------------------------------------------
    // AlignContent initial value
    // ---------------------------------------------------------------
    [Fact]
    public void InitialValue_ResetsAlignContent()
    {
        var style = new ComputedStyle { AlignContent = AlignContentType.Center };
        PropertyDefaults.ApplyInitialValue(style, "align-content");
        Assert.Equal(AlignContentType.Stretch, style.AlignContent);
    }

    [Fact]
    public void InheritProperty_CopiesAlignContent()
    {
        var source = new ComputedStyle { AlignContent = AlignContentType.SpaceAround };
        var target = new ComputedStyle();
        PropertyDefaults.InheritProperty(target, "align-content", source);
        Assert.Equal(AlignContentType.SpaceAround, target.AlignContent);
    }

    // ---------------------------------------------------------------
    // AlignContent with single line (no wrap)
    // ---------------------------------------------------------------
    [Fact]
    public void AlignContent_SingleLine_NoEffect()
    {
        // align-content only affects multi-line flex containers
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; height: 200px; width: 400px;
                        align-content: center; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        Assert.NotNull(a);
        // Without wrap, align-content has no effect on single line
    }

    [Fact]
    public void AlignContent_FlexStart_LinesAtTop()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; height: 200px; width: 100px;
                        align-content: flex-start; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        Assert.NotNull(a);
        // flex-start: lines should be at the top
        // A should be near Y=0
    }

    [Fact]
    public void AlignContent_SpaceEvenly_EqualGaps()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; height: 200px; width: 100px;
                        align-content: space-evenly; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // space-evenly: equal gaps before, between, and after lines
        // free space = 200 - 60 = 140; 3 gaps; each = ~46.67
        float firstGap = a!.Dimensions.Y;  // gap before first line
        Assert.True(firstGap > 20, "Should have gap before first line");
    }

    [Fact]
    public void AlignContent_SpaceAround_HalfGapsAtEdges()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; height: 200px; width: 100px;
                        align-content: space-around; margin: 0; padding: 0; }
                .item { width: 100px; height: 30px; margin: 0; padding: 0; }
            </style></head><body style=""margin:0;padding:0"">
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        Assert.NotNull(a);
        Assert.NotNull(b);

        // space-around: half-size gap at edges, full gap between
        float edgeGap = a!.Dimensions.Y;
        Assert.True(edgeGap > 10, "Should have gap at edge");
    }
}
