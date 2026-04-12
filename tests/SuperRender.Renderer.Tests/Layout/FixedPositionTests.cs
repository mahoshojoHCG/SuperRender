using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class FixedPositionTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800, float viewportHeight = 600)
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(html);

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);

        var engine = new LayoutEngine(measurer);
        return engine.BuildLayoutTree(doc, styles, viewportWidth, viewportHeight);
    }

    [Fact]
    public void Fixed_ParsesFromCss()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .fix { position: fixed; top: 0; left: 0; width: 100px; height: 50px; }
            </style></head><body><div class='fix'>fixed</div></body></html>");

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        Assert.Equal(PositionType.Fixed, box!.Style.Position);
    }

    [Fact]
    public void Fixed_PositionedRelativeToViewport_TopLeft()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .fix { position: fixed; top: 10px; left: 20px; width: 100px; height: 50px; }
            </style></head><body><div class='fix'>fixed</div></body></html>");

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        // Fixed elements are positioned relative to the viewport (0,0)
        Assert.Equal(20f, box!.Dimensions.X, 1f);
        Assert.Equal(10f, box.Dimensions.Y, 1f);
    }

    [Fact]
    public void Fixed_PositionedRelativeToViewport_BottomRight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .fix { position: fixed; bottom: 10px; right: 20px; width: 100px; height: 50px; }
            </style></head><body><div class='fix'>fixed</div></body></html>",
            800, 600);

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        // Bottom-right positioning: X = 800 - 20 - 100 = 680, Y depends on height
        Assert.Equal(100f, box!.Dimensions.Width, 1f);
        Assert.True(box.Dimensions.X > 600f, "Fixed element should be near right edge of viewport");
    }

    [Fact]
    public void Fixed_DoesNotAffectNormalFlow()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .fix { position: fixed; top: 0; left: 0; width: 100px; height: 50px; }
                .normal { width: 200px; height: 40px; }
            </style></head><body>
                <div class='fix'>fixed</div>
                <div class='normal'>normal</div>
            </body></html>");

        var normalBox = FindBoxByClass(root, "normal");
        Assert.NotNull(normalBox);
        // Normal element should not be pushed down by the fixed element
        Assert.Equal(0f, normalBox!.Dimensions.Y, 1f);
    }

    [Fact]
    public void Fixed_Width_Respected()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .fix { position: fixed; top: 0; left: 0; width: 200px; height: 100px; }
            </style></head><body><div class='fix'>text</div></body></html>");

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        Assert.Equal(200f, box!.Dimensions.Width, 1f);
    }

    [Fact]
    public void Fixed_Height_Respected()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .fix { position: fixed; top: 0; left: 0; width: 200px; height: 100px; }
            </style></head><body><div class='fix'>text</div></body></html>");

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        Assert.Equal(100f, box!.Dimensions.Height, 1f);
    }

    [Fact]
    public void Fixed_InsideNestedContainer_StillRelativeToViewport()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .container { margin: 50px; padding: 50px; }
                .fix { position: fixed; top: 5px; left: 5px; width: 80px; height: 40px; }
            </style></head><body>
                <div class='container'>
                    <div class='fix'>fixed</div>
                </div>
            </body></html>");

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        // Even though the fixed element is inside a container with margin+padding,
        // it should be positioned relative to viewport origin
        Assert.Equal(5f, box!.Dimensions.X, 1f);
        Assert.Equal(5f, box.Dimensions.Y, 1f);
    }

    [Fact]
    public void Fixed_WidthAutoUsesViewportWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .fix { position: fixed; top: 0; left: 0; }
            </style></head><body><div class='fix'>text</div></body></html>",
            800, 600);

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        // Auto width should use viewport width as containing block
        Assert.Equal(800f, box!.Dimensions.Width, 1f);
    }

    [Fact]
    public void Fixed_LeftAndRightDeterminesWidth()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .fix { position: fixed; top: 0; left: 50px; right: 50px; height: 40px; }
            </style></head><body><div class='fix'>text</div></body></html>",
            800, 600);

        var box = FindBoxByClass(root, "fix");
        Assert.NotNull(box);
        // Width = viewport width - left - right = 800 - 50 - 50 = 700
        Assert.Equal(700f, box!.Dimensions.Width, 1f);
    }

    [Fact]
    public void Fixed_MultipleFixedElements()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .fix1 { position: fixed; top: 0; left: 0; width: 100px; height: 50px; }
                .fix2 { position: fixed; top: 60px; left: 0; width: 100px; height: 50px; }
            </style></head><body>
                <div class='fix1'>first</div>
                <div class='fix2'>second</div>
            </body></html>");

        var box1 = FindBoxByClass(root, "fix1");
        var box2 = FindBoxByClass(root, "fix2");
        Assert.NotNull(box1);
        Assert.NotNull(box2);
        Assert.Equal(0f, box1!.Dimensions.Y, 1f);
        Assert.Equal(60f, box2!.Dimensions.Y, 1f);
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
