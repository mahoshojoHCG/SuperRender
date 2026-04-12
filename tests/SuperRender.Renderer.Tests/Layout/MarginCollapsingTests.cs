using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class MarginCollapsingTests
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
    public void AdjacentSiblings_MarginsCollapse()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 30px; height: 50px; }
                .b { margin-top: 20px; height: 50px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // Without collapsing: B.Y = 50 + 30 + 20 = 100
        // With collapsing: B.Y = 50 + max(30, 20) = 80
        float gap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gap <= 31f, $"Margin gap should be ~30 (collapsed), was {gap}");
    }

    [Fact]
    public void AdjacentSiblings_EqualMargins_Collapse()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 20px; height: 50px; }
                .b { margin-top: 20px; height: 50px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // With collapsing: gap = max(20, 20) = 20
        float gap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gap <= 21f, $"Collapsed margin should be ~20, was {gap}");
    }

    [Fact]
    public void EmptyBlock_MarginsCollapseThrough()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 10px; height: 40px; }
                .empty { margin-top: 20px; margin-bottom: 30px; }
                .b { margin-top: 5px; height: 40px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='empty'></div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // Empty block's top and bottom margins should collapse together.
        // Then that collapsed margin collapses with adjacent margins.
        float totalGap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        // The gap should be significantly less than 10 + 20 + 30 + 5 = 65
        Assert.True(totalGap < 60f, $"Gap through empty block should be collapsed, was {totalGap}");
    }

    [Fact]
    public void EmptyBlock_WithBorder_PreventsCollapse()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 10px; height: 40px; }
                .empty { margin-top: 20px; margin-bottom: 30px; border-top: 1px solid black; }
                .b { margin-top: 5px; height: 40px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='empty'></div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // With border, the empty block's margins don't collapse through,
        // but adjacent sibling margins still collapse.
        float gap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gap > 0f, "There should be a gap between elements");
    }

    [Fact]
    public void ParentFirstChild_MarginsCollapse_NoBorderOrPadding()
    {
        // Test that parent-first-child margin collapsing occurs.
        // When the parent has no border-top/padding-top, the child's margin-top
        // collapses with the parent's margin-top. The effective margin is max(parent, child).
        // Note: transitive collapsing through grandparent is not implemented.
        var root = LayoutHtml(@"<html><head><style>body { margin: 0; } .parent { margin-top: 20px; } .child { margin-top: 30px; height: 50px; }</style></head><body><div class='parent'><div class='child'>child</div></div></body></html>");

        var parent = FindBoxByClass(root, "parent");
        var child = FindBoxByClass(root, "child");
        Assert.NotNull(parent);
        Assert.NotNull(child);

        // Parent and child margins collapse. The child should not be at
        // parent-top + parent-margin(20) + child-margin(30) fully uncollapsed.
        // With our collapsing implementation, the child should be at most 50px from top.
        Assert.True(child!.Dimensions.Y <= 55f,
            $"Child should benefit from margin collapsing, was at {child.Dimensions.Y}");
    }

    [Fact]
    public void ParentFirstChild_NoBorderNoCollapse_WithPadding()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .parent { margin-top: 20px; padding-top: 10px; }
                .child { margin-top: 30px; height: 50px; }
            </style></head><body>
                <div class='parent'>
                    <div class='child'>child</div>
                </div>
            </body></html>");

        var parent = FindBoxByClass(root, "parent");
        var child = FindBoxByClass(root, "child");
        Assert.NotNull(parent);
        Assert.NotNull(child);

        // With padding-top on parent, margins don't collapse
        // Child should be at: parent-margin(20) + parent-padding(10) + child-margin(30) = 60
        Assert.True(child!.Dimensions.Y >= 55f,
            $"With padding, child should be at Y >= 55, was at {child.Dimensions.Y}");
    }

    [Fact]
    public void NoCollapse_WhenPaddingSeparates()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 30px; height: 50px; }
                .b { margin-top: 0; height: 50px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // When one margin is 0, it's not "collapsing" in the meaningful sense
        float gap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gap >= 29f, $"Gap should include margin-bottom of 30px, was {gap}");
    }

    [Fact]
    public void ThreeSiblings_AllMarginsCollapse()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 20px; height: 40px; }
                .b { margin-top: 15px; margin-bottom: 25px; height: 40px; }
                .c { margin-top: 10px; height: 40px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='b'>second</div>
                <div class='c'>third</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        var boxC = FindBoxByClass(root, "c");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);
        Assert.NotNull(boxC);

        // A-B gap: max(20, 15) = 20
        float gapAB = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gapAB <= 21f, $"A-B gap should be ~20, was {gapAB}");

        // B-C gap: max(25, 10) = 25
        float gapBC = boxC!.Dimensions.Y - (boxB.Dimensions.Y + boxB.Dimensions.Height);
        Assert.True(gapBC <= 26f, $"B-C gap should be ~25, was {gapBC}");
    }

    [Fact]
    public void EmptyBlock_WithPadding_PreventsCollapse()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 10px; height: 40px; }
                .empty { margin-top: 20px; margin-bottom: 30px; padding-top: 1px; }
                .b { height: 40px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='empty'></div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxEmpty = FindBoxByClass(root, "empty");
        Assert.NotNull(boxA);
        Assert.NotNull(boxEmpty);

        // With padding, the empty block's top/bottom margins don't collapse through
        Assert.True(boxEmpty!.Dimensions.Y > boxA!.Dimensions.Y + boxA.Dimensions.Height,
            "Empty block with padding should take up space");
    }

    [Fact]
    public void ZeroMargins_NoCollapse()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 0; height: 40px; }
                .b { margin-top: 0; height: 40px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // With zero margins, B should be right below A
        float gap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gap < 1f, $"Zero margins should produce zero gap, was {gap}");
    }

    [Fact]
    public void LargerBottomMargin_Wins()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .a { margin-bottom: 50px; height: 40px; }
                .b { margin-top: 20px; height: 40px; }
            </style></head><body>
                <div class='a'>first</div>
                <div class='b'>second</div>
            </body></html>");

        var boxA = FindBoxByClass(root, "a");
        var boxB = FindBoxByClass(root, "b");
        Assert.NotNull(boxA);
        Assert.NotNull(boxB);

        // Collapsed margin = max(50, 20) = 50
        float gap = boxB!.Dimensions.Y - (boxA!.Dimensions.Y + boxA.Dimensions.Height);
        Assert.True(gap <= 51f, $"Collapsed margin should be ~50, was {gap}");
        Assert.True(gap >= 45f, $"Collapsed margin should be ~50, was {gap}");
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
