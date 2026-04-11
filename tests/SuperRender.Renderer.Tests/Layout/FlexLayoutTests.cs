using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class FlexLayoutTests
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
    // 1. Basic flex container (row direction)
    // ---------------------------------------------------------------
    [Fact]
    public void FlexContainer_Row_ItemsLaidOutHorizontally()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // Items should be horizontal: a.X < b.X < c.X
        Assert.True(a!.Dimensions.X < b!.Dimensions.X);
        Assert.True(b.Dimensions.X < c!.Dimensions.X);

        // All on the same row (same Y)
        Assert.Equal(a.Dimensions.Y, b.Dimensions.Y, 0.1f);
        Assert.Equal(b.Dimensions.Y, c.Dimensions.Y, 0.1f);

        // Each item should be 100px wide
        Assert.Equal(100, a.Dimensions.Width, 0.1f);
        Assert.Equal(100, b.Dimensions.Width, 0.1f);
        Assert.Equal(100, c.Dimensions.Width, 0.1f);
    }

    // ---------------------------------------------------------------
    // 2. flex-direction: column
    // ---------------------------------------------------------------
    [Fact]
    public void FlexDirection_Column_ItemsStackedVertically()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-direction: column; width: 400px; }
                .item { height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // Items should be stacked vertically: a.Y < b.Y
        Assert.True(a!.Dimensions.Y < b!.Dimensions.Y);

        // Same X position
        Assert.Equal(a.Dimensions.X, b.Dimensions.X, 0.1f);
    }

    // ---------------------------------------------------------------
    // 3. flex-direction: row-reverse
    // ---------------------------------------------------------------
    [Fact]
    public void FlexDirection_RowReverse_ItemsRightToLeft()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-direction: row-reverse; width: 600px; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // In row-reverse: items placed right-to-left: c.X < b.X < a.X
        Assert.True(a!.Dimensions.X > b!.Dimensions.X);
        Assert.True(b.Dimensions.X > c!.Dimensions.X);
    }

    // ---------------------------------------------------------------
    // 4. flex-direction: column-reverse
    // ---------------------------------------------------------------
    [Fact]
    public void FlexDirection_ColumnReverse_ItemsBottomToTop()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-direction: column-reverse; width: 400px; height: 200px; }
                .item { height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // In column-reverse: first item rendered after second
        Assert.True(a!.Dimensions.Y > b!.Dimensions.Y);
    }

    // ---------------------------------------------------------------
    // 5. flex-grow
    // ---------------------------------------------------------------
    [Fact]
    public void FlexGrow_ItemsGrowProportionally()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { flex-grow: 1; height: 50px; }
                .b { flex-grow: 2; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // B should be roughly twice A's width
        Assert.True(b!.Dimensions.Width > a!.Dimensions.Width);
        float ratio = b.Dimensions.Width / a.Dimensions.Width;
        Assert.True(ratio > 1.5f && ratio < 2.5f, $"Expected ratio near 2, got {ratio}");
    }

    // ---------------------------------------------------------------
    // 6. flex-shrink
    // ---------------------------------------------------------------
    [Fact]
    public void FlexShrink_ItemsShrinkWhenOverflowing()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 300px; }
                .a { width: 200px; flex-shrink: 1; height: 50px; }
                .b { width: 200px; flex-shrink: 1; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // Total = 400px but container = 300px.
        // With equal shrink ratios, each should be shrunk to 150px
        Assert.Equal(a!.Dimensions.Width, b!.Dimensions.Width, 0.1f);
        Assert.True(a.Dimensions.Width < 200, $"Expected shrunk width, got {a.Dimensions.Width}");
    }

    // ---------------------------------------------------------------
    // 7. flex-basis
    // ---------------------------------------------------------------
    [Fact]
    public void FlexBasis_ExplicitBaseSizeRespected()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { flex-basis: 200px; height: 50px; }
                .b { flex-basis: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        Assert.Equal(200, a!.Dimensions.Width, 0.1f);
        Assert.Equal(100, b!.Dimensions.Width, 0.1f);
    }

    // ---------------------------------------------------------------
    // 8. justify-content: center
    // ---------------------------------------------------------------
    [Fact]
    public void JustifyContent_Center_ItemsCenteredOnMainAxis()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; justify-content: center; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(flex);
        Assert.NotNull(a);
        Assert.NotNull(b);

        // Total items = 200px, container = 600px, free = 400px
        // Center: 200px offset on each side
        float flexStart = flex!.Dimensions.X;
        float expectedOffset = (600 - 200) / 2f;
        float aStartRelative = a!.Dimensions.X - flexStart;
        Assert.True(Math.Abs(aStartRelative - expectedOffset) < 2f,
            $"Expected ~{expectedOffset} from flex start, got {aStartRelative}");
    }

    // ---------------------------------------------------------------
    // 9. justify-content: space-between
    // ---------------------------------------------------------------
    [Fact]
    public void JustifyContent_SpaceBetween_FirstAndLastAtEdges()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; justify-content: space-between; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        var a = FindBoxByClass(root, "a");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(flex);
        Assert.NotNull(a);
        Assert.NotNull(c);

        float flexStart = flex!.Dimensions.X;
        float flexEnd = flexStart + 600;

        // First item at the start
        Assert.True(Math.Abs(a!.Dimensions.X - flexStart) < 1f);

        // Last item's right edge at the end of the container
        float cRight = c!.Dimensions.X + c.Dimensions.Width;
        Assert.True(Math.Abs(cRight - flexEnd) < 1f, $"Expected right edge at {flexEnd}, got {cRight}");
    }

    // ---------------------------------------------------------------
    // 10. justify-content: space-around
    // ---------------------------------------------------------------
    [Fact]
    public void JustifyContent_SpaceAround_EqualSpaceAround()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; justify-content: space-around; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(flex);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // Free space = 300px, 3 items: each gets 100px around
        // Half-space at edges (50px), full space between items (100px)
        float flexStart = flex!.Dimensions.X;
        float aStart = a!.Dimensions.X - flexStart;
        float gap1 = b!.Dimensions.X - (a.Dimensions.X + a.Dimensions.Width);
        float gap2 = c!.Dimensions.X - (b.Dimensions.X + b.Dimensions.Width);

        // Gaps between items should be roughly equal
        Assert.True(Math.Abs(gap1 - gap2) < 2f, $"Gaps should be equal: {gap1} vs {gap2}");
        // Edge space should be roughly half the gap
        Assert.True(aStart > 10f, $"Expected some padding at the start, got {aStart}");
    }

    // ---------------------------------------------------------------
    // 11. justify-content: space-evenly
    // ---------------------------------------------------------------
    [Fact]
    public void JustifyContent_SpaceEvenly_EqualSpaceBetween()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 400px; justify-content: space-evenly; }
                .item { width: 50px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(flex);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // Free space = 400 - 150 = 250px, 4 gaps (start, between 3, end)
        // Each gap = 250/4 = 62.5px
        float flexStart = flex!.Dimensions.X;
        float startGap = a!.Dimensions.X - flexStart;
        float gap1 = b!.Dimensions.X - (a.Dimensions.X + a.Dimensions.Width);
        float gap2 = c!.Dimensions.X - (b.Dimensions.X + b.Dimensions.Width);

        Assert.True(Math.Abs(startGap - gap1) < 2f, $"Start gap ({startGap}) should equal gap1 ({gap1})");
        Assert.True(Math.Abs(gap1 - gap2) < 2f, $"Gap1 ({gap1}) should equal gap2 ({gap2})");
    }

    // ---------------------------------------------------------------
    // 12. justify-content: flex-end
    // ---------------------------------------------------------------
    [Fact]
    public void JustifyContent_FlexEnd_ItemsPackedToEnd()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; justify-content: flex-end; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(flex);
        Assert.NotNull(b);

        // Last item's right edge should be at the container's right edge
        float flexEnd = flex!.Dimensions.X + flex.Dimensions.Width;
        float bRight = b!.Dimensions.X + b.Dimensions.Width;
        Assert.True(Math.Abs(bRight - flexEnd) < 1f,
            $"Expected right edge at {flexEnd}, got {bRight}");
    }

    // ---------------------------------------------------------------
    // 13. align-items: center
    // ---------------------------------------------------------------
    [Fact]
    public void AlignItems_Center_ItemsCenteredOnCrossAxis()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; align-items: center; }
                .a { width: 100px; height: 30px; }
                .b { width: 100px; height: 60px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(flex);
        Assert.NotNull(a);
        Assert.NotNull(b);

        // Line cross size = 60 (max item height)
        // A (height=30) should be centered: offset = (60-30)/2 = 15 from flex start
        float flexY = flex!.Dimensions.Y;
        float aOffsetFromFlex = a!.Dimensions.Y - flexY;
        float bOffsetFromFlex = b!.Dimensions.Y - flexY;

        // B is the tallest, so it should be at the top
        Assert.True(Math.Abs(bOffsetFromFlex) < 1f, $"B offset: {bOffsetFromFlex}");
        // A should be centered: offset ~15
        Assert.True(aOffsetFromFlex > 10f, $"A should be centered, offset: {aOffsetFromFlex}");
    }

    // ---------------------------------------------------------------
    // 14. align-items: flex-start
    // ---------------------------------------------------------------
    [Fact]
    public void AlignItems_FlexStart_ItemsAlignedToTopOfCrossAxis()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; align-items: flex-start; }
                .a { width: 100px; height: 30px; }
                .b { width: 100px; height: 60px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // Both should start at the same Y
        Assert.Equal(a!.Dimensions.Y, b!.Dimensions.Y, 0.1f);
    }

    // ---------------------------------------------------------------
    // 15. align-items: flex-end
    // ---------------------------------------------------------------
    [Fact]
    public void AlignItems_FlexEnd_ItemsAlignedToBottomOfCrossAxis()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; align-items: flex-end; }
                .a { width: 100px; height: 30px; }
                .b { width: 100px; height: 60px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // Bottom edges should be at the same Y
        float aBottom = a!.Dimensions.Y + a.Dimensions.Height;
        float bBottom = b!.Dimensions.Y + b.Dimensions.Height;
        Assert.Equal(aBottom, bBottom, 1f);
    }

    // ---------------------------------------------------------------
    // 16. align-items: stretch (default)
    // ---------------------------------------------------------------
    [Fact]
    public void AlignItems_Stretch_ItemsStretchToCrossAxisSize()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { width: 100px; }
                .b { width: 100px; height: 80px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // A has no explicit height, so with stretch it should match the line cross size
        // Line cross size is determined by B's height (80)
        Assert.Equal(80, a!.Dimensions.Height, 1f);
        Assert.Equal(80, b!.Dimensions.Height, 0.1f);
    }

    // ---------------------------------------------------------------
    // 17. gap
    // ---------------------------------------------------------------
    [Fact]
    public void Gap_AddsSpaceBetweenItems()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; gap: 20px; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        float gap1 = b!.Dimensions.X - (a!.Dimensions.X + a.Dimensions.Width);
        float gap2 = c!.Dimensions.X - (b.Dimensions.X + b.Dimensions.Width);

        Assert.Equal(20, gap1, 1f);
        Assert.Equal(20, gap2, 1f);
    }

    // ---------------------------------------------------------------
    // 18. flex-wrap: wrap
    // ---------------------------------------------------------------
    [Fact]
    public void FlexWrap_Wrap_ItemsWrapToNextLine()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; width: 250px; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // A and B fit on first line (200px < 250px), C wraps
        Assert.Equal(a!.Dimensions.Y, b!.Dimensions.Y, 0.1f);
        Assert.True(c!.Dimensions.Y > a.Dimensions.Y,
            $"C ({c.Dimensions.Y}) should be on a new line below A ({a.Dimensions.Y})");
    }

    // ---------------------------------------------------------------
    // 19. flex: 1 shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void FlexShorthand_Flex1_ItemsShareSpaceEqually()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .item { flex: 1; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // All items should get equal width: 200px each
        Assert.Equal(200, a!.Dimensions.Width, 1f);
        Assert.Equal(200, b!.Dimensions.Width, 1f);
        Assert.Equal(200, c!.Dimensions.Width, 1f);
    }

    // ---------------------------------------------------------------
    // 20. Nested flex
    // ---------------------------------------------------------------
    [Fact]
    public void NestedFlex_InnerFlexContainerLaidOut()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .outer { display: flex; width: 600px; }
                .inner { display: flex; flex: 1; height: 50px; }
                .item { width: 50px; height: 50px; }
            </style></head><body>
                <div class=""outer"">
                    <div class=""inner"">
                        <div class=""item a"">A</div>
                        <div class=""item b"">B</div>
                    </div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(c);

        // A and C should both be on the same row
        Assert.Equal(a!.Dimensions.Y, c!.Dimensions.Y, 0.1f);
    }

    // ---------------------------------------------------------------
    // 21. Flex with fixed width items
    // ---------------------------------------------------------------
    [Fact]
    public void FlexItem_WidthPropertyRespectedAsBasisFallback()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { width: 150px; height: 50px; }
                .b { width: 250px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        Assert.Equal(150, a!.Dimensions.Width, 0.1f);
        Assert.Equal(250, b!.Dimensions.Width, 0.1f);
    }

    // ---------------------------------------------------------------
    // 22. Flex container height (auto height)
    // ---------------------------------------------------------------
    [Fact]
    public void FlexContainer_AutoHeight_DeterminedByTallestItem()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { width: 100px; height: 30px; }
                .b { width: 100px; height: 60px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);

        // Height should be at least 60 (tallest item)
        Assert.True(flex!.Dimensions.Height >= 60, $"Flex height: {flex.Dimensions.Height}");
    }

    // ---------------------------------------------------------------
    // 23. Flex container explicit height
    // ---------------------------------------------------------------
    [Fact]
    public void FlexContainer_ExplicitHeight_Respected()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; height: 200px; }
                .item { width: 100px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item"">A</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(200, flex!.Dimensions.Height, 0.1f);
    }

    // ---------------------------------------------------------------
    // 24. flex-grow with different ratios
    // ---------------------------------------------------------------
    [Fact]
    public void FlexGrow_DifferentRatios_SpaceDistributedProperly()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 500px; }
                .a { flex-grow: 1; flex-basis: 0; height: 50px; }
                .b { flex-grow: 3; flex-basis: 0; height: 50px; }
                .c { flex-grow: 1; flex-basis: 0; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                    <div class=""c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // Total grow = 5, container = 500px
        // A: 100px, B: 300px, C: 100px
        Assert.Equal(100, a!.Dimensions.Width, 1f);
        Assert.Equal(300, b!.Dimensions.Width, 1f);
        Assert.Equal(100, c!.Dimensions.Width, 1f);
    }

    // ---------------------------------------------------------------
    // 25. flex: none shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void FlexShorthand_None_NoGrowNoShrink()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { flex: none; width: 200px; height: 50px; }
                .b { flex: none; width: 200px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // Items should not grow despite extra space (600 - 400 = 200 free)
        Assert.Equal(200, a!.Dimensions.Width, 0.1f);
        Assert.Equal(200, b!.Dimensions.Width, 0.1f);
    }

    // ---------------------------------------------------------------
    // 26. align-self overrides align-items
    // ---------------------------------------------------------------
    [Fact]
    public void AlignSelf_OverridesContainerAlignItems()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; align-items: flex-start; }
                .a { width: 100px; height: 30px; }
                .b { width: 100px; height: 30px; align-self: flex-end; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // With equal heights and flex-start default, items start at same Y
        // B with align-self: flex-end should be at the bottom of the line
        Assert.Equal(AlignSelfType.FlexEnd, b!.Style.AlignSelf);
    }

    // ---------------------------------------------------------------
    // 27. flex-flow shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void FlexFlow_SetsDirectionAndWrap()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-flow: column wrap; width: 200px; height: 200px; }
                .item { width: 50px; height: 80px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(FlexDirectionType.Column, flex!.Style.FlexDirection);
        Assert.Equal(FlexWrapType.Wrap, flex.Style.FlexWrap);
    }

    // ---------------------------------------------------------------
    // 28. Flex with padding and margins
    // ---------------------------------------------------------------
    [Fact]
    public void FlexItem_PaddingAndMarginRespected()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { width: 100px; height: 50px; margin: 10px; padding: 5px; }
                .b { width: 100px; height: 50px; margin: 10px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // B should start after A's outer width (100 + 10 + 10 + 5 + 5 = 130) + B's margin
        Assert.True(b!.Dimensions.X > a!.Dimensions.X + a.Dimensions.Width);

        // A should have padding
        Assert.Equal(5, a.Dimensions.Padding.Left, 0.1f);
        Assert.Equal(5, a.Dimensions.Padding.Top, 0.1f);
    }

    // ---------------------------------------------------------------
    // 29. row-gap and column-gap
    // ---------------------------------------------------------------
    [Fact]
    public void RowGapColumnGap_AppliedSeparately()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-wrap: wrap; width: 250px; row-gap: 30px; column-gap: 10px; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">A</div>
                    <div class=""item b"">B</div>
                    <div class=""item c"">C</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");
        var c = FindBoxByClass(root, "c");

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);

        // A and B on first line with column-gap between them
        float colGap = b!.Dimensions.X - (a!.Dimensions.X + a.Dimensions.Width);
        Assert.Equal(10, colGap, 1f);

        // C on second line, with row-gap above
        float rowGap = c!.Dimensions.Y - (a.Dimensions.Y + a.Dimensions.Height);
        Assert.Equal(30, rowGap, 1f);
    }

    // ---------------------------------------------------------------
    // 30. Flex container as child of block
    // ---------------------------------------------------------------
    [Fact]
    public void FlexContainer_InsideBlockParent_FullWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .wrapper { width: 500px; }
                .flex { display: flex; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""wrapper"">
                    <div class=""flex"">
                        <div class=""item a"">A</div>
                        <div class=""item b"">B</div>
                    </div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);

        // Flex container should fill parent width
        Assert.Equal(500, flex!.Dimensions.Width, 0.1f);
    }

    // ---------------------------------------------------------------
    // 31. flex: auto shorthand
    // ---------------------------------------------------------------
    [Fact]
    public void FlexShorthand_Auto_GrowsShrinks()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { flex: auto; width: 100px; height: 50px; }
                .b { flex: auto; width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // flex: auto = grow:1 shrink:1 basis:auto
        // Both items start at 100px, grow equally with 400px remaining
        // Each gets 200px extra: final = 300px
        Assert.Equal(a!.Dimensions.Width, b!.Dimensions.Width, 1f);
        Assert.True(a.Dimensions.Width > 100, $"Expected item to grow from 100, got {a.Dimensions.Width}");
    }

    // ---------------------------------------------------------------
    // 32. Display flex sets BoxType correctly
    // ---------------------------------------------------------------
    [Fact]
    public void DisplayFlex_CreatesFlexContainerBoxType()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; }
            </style></head><body>
                <div class=""flex"">
                    <div>A</div>
                </div>
            </body></html>");

        var flex = FindBoxByClass(root, "flex");
        Assert.NotNull(flex);
        Assert.Equal(LayoutBoxType.FlexContainer, flex!.BoxType);
    }

    // ---------------------------------------------------------------
    // 33. Column direction with flex-grow
    // ---------------------------------------------------------------
    [Fact]
    public void FlexDirection_Column_FlexGrowDistributesVerticalSpace()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; flex-direction: column; width: 300px; height: 300px; }
                .a { flex-grow: 1; }
                .b { flex-grow: 2; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                    <div class=""b"">B</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // B should be roughly twice the height of A
        Assert.True(b!.Dimensions.Height > a!.Dimensions.Height,
            $"B height ({b.Dimensions.Height}) should be greater than A ({a.Dimensions.Height})");
    }

    // ---------------------------------------------------------------
    // 34. flex shorthand 3-value form
    // ---------------------------------------------------------------
    [Fact]
    public void FlexShorthand_ThreeValues_Parsed()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .a { flex: 1 0 200px; height: 50px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""a"">A</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        Assert.NotNull(a);

        // flex: 1 0 200px -> basis 200, grow 1, shrink 0
        // Since it's the only item in 600px container, it grows to 600
        Assert.Equal(600, a!.Dimensions.Width, 1f);
    }

    // ---------------------------------------------------------------
    // 35. Flex items with text content
    // ---------------------------------------------------------------
    [Fact]
    public void FlexItem_WithTextContent_SizedByContent()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 600px; }
                .item { height: 30px; }
            </style></head><body>
                <div class=""flex"">
                    <div class=""item a"">Hello</div>
                    <div class=""item b"">World</div>
                </div>
            </body></html>");

        var a = FindBoxByClass(root, "a");
        var b = FindBoxByClass(root, "b");

        Assert.NotNull(a);
        Assert.NotNull(b);

        // Items should be horizontal
        Assert.True(b!.Dimensions.X > a!.Dimensions.X);
    }

    // ---------------------------------------------------------------
    // 36. Multiple flex containers
    // ---------------------------------------------------------------
    [Fact]
    public void MultipleFlexContainers_IndependentLayout()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .flex { display: flex; width: 400px; }
                .item { width: 100px; height: 50px; }
            </style></head><body>
                <div class=""flex f1"">
                    <div class=""item"">A</div>
                    <div class=""item"">B</div>
                </div>
                <div class=""flex f2"">
                    <div class=""item"">C</div>
                    <div class=""item"">D</div>
                </div>
            </body></html>");

        var f1 = FindBoxByClass(root, "f1");
        var f2 = FindBoxByClass(root, "f2");

        Assert.NotNull(f1);
        Assert.NotNull(f2);

        // Second flex container should be below the first
        Assert.True(f2!.Dimensions.Y > f1!.Dimensions.Y);
    }
}
