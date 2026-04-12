using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class StickyPositionTests
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
    public void Sticky_ParsesFromCss()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .sticky { position: sticky; top: 10px; width: 100px; height: 50px; }
            </style></head><body><div class='sticky'>sticky</div></body></html>");

        var box = FindBoxByClass(root, "sticky");
        Assert.NotNull(box);
        Assert.Equal(PositionType.Sticky, box!.Style.Position);
    }

    [Fact]
    public void Sticky_BehavesLikeRelative_InStaticLayout()
    {
        // Sticky degrades to relative in static layout (no scroll context)
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .before { width: 100px; height: 100px; }
                .sticky { position: sticky; top: 10px; width: 100px; height: 50px; }
            </style></head><body>
                <div class='before'>before</div>
                <div class='sticky'>sticky</div>
            </body></html>");

        var stickyBox = FindBoxByClass(root, "sticky");
        Assert.NotNull(stickyBox);
        // Sticky with top:10px should behave like relative with top:10px
        // Normal position would be at Y=100 (after 'before' div), relative shifts by +10
        Assert.True(stickyBox!.Dimensions.Y >= 110f,
            $"Sticky element should be at Y >= 110, was at {stickyBox.Dimensions.Y}");
    }

    [Fact]
    public void Sticky_DoesNotRemoveFromFlow()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .sticky { position: sticky; top: 10px; width: 100px; height: 50px; }
                .after { width: 100px; height: 30px; }
            </style></head><body>
                <div class='sticky'>sticky</div>
                <div class='after'>after</div>
            </body></html>");

        var afterBox = FindBoxByClass(root, "after");
        Assert.NotNull(afterBox);
        // The 'after' element should be positioned after the sticky element's normal flow position
        // sticky element occupies 50px height in flow
        Assert.True(afterBox!.Dimensions.Y >= 50f,
            $"Element after sticky should be at Y >= 50, was at {afterBox.Dimensions.Y}");
    }

    [Fact]
    public void Sticky_WithLeftOffset_BehavesLikeRelative()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                body { margin: 0; }
                .sticky { position: sticky; left: 20px; width: 100px; height: 50px; }
            </style></head><body>
                <div class='sticky'>sticky</div>
            </body></html>");

        var box = FindBoxByClass(root, "sticky");
        Assert.NotNull(box);
        // With left:20px, the element should be offset 20px to the right from normal position
        Assert.True(box!.Dimensions.X >= 20f,
            $"Sticky with left:20px should be at X >= 20, was at {box.Dimensions.X}");
    }

    [Fact]
    public void Sticky_IsNotStatic()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .sticky { position: sticky; top: 0; }
            </style></head><body><div class='sticky'>sticky</div></body></html>");

        var box = FindBoxByClass(root, "sticky");
        Assert.NotNull(box);
        Assert.NotEqual(PositionType.Static, box!.Style.Position);
    }

    [Fact]
    public void Sticky_TopValue_Stored()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .sticky { position: sticky; top: 30px; width: 100px; height: 50px; }
            </style></head><body><div class='sticky'>sticky</div></body></html>");

        var box = FindBoxByClass(root, "sticky");
        Assert.NotNull(box);
        Assert.Equal(30f, box!.Style.Top, 0.1f);
    }

    [Fact]
    public void Sticky_HasDimensions()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .sticky { position: sticky; top: 0; width: 200px; height: 80px; }
            </style></head><body><div class='sticky'>sticky</div></body></html>");

        var box = FindBoxByClass(root, "sticky");
        Assert.NotNull(box);
        Assert.Equal(200f, box!.Dimensions.Width, 1f);
        Assert.Equal(80f, box.Dimensions.Height, 1f);
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
