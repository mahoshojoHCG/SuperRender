using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class PositionTests
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
    public void Relative_OffsetByLeftAndTop()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .rel { position: relative; left: 10px; top: 20px; width: 100px; height: 50px; }
            </style></head><body><div class='rel'>text</div></body></html>");

        var box = FindBoxByClass(root, "rel");
        Assert.NotNull(box);

        // The box should be offset from its normal-flow position
        // Normal position: X is at container X + margins etc.
        // With left:10px, top:20px, the position should be shifted
        // We can check that the position includes the offset
        Assert.True(box!.Dimensions.X >= 10f);
        Assert.True(box.Dimensions.Y >= 20f);
    }

    [Fact]
    public void Relative_Offset_AppliedToBox()
    {
        // Verify that a relatively positioned element has its offset applied
        var root = LayoutHtml(@"
            <html><head><style>
                .rel { position: relative; top: 50px; width: 100px; height: 30px; }
            </style></head><body>
                <div class='rel'>first</div>
            </body></html>");

        var relBox = FindBoxByClass(root, "rel");
        Assert.NotNull(relBox);

        // The element should be offset from the container top by at least 50px
        Assert.True(relBox!.Dimensions.Y >= 50f);
    }

    [Fact]
    public void Absolute_RemovedFromNormalFlow()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                .container { position: relative; width: 400px; height: 300px; }
                .abs { position: absolute; top: 10px; left: 10px; width: 100px; height: 50px; }
                .normal { width: 200px; height: 40px; }
            </style></head><body>
                <div class='container'>
                    <div class='abs'>absolute</div>
                    <div class='normal'>normal</div>
                </div>
            </body></html>");

        var normalBox = FindBoxByClass(root, "normal");
        Assert.NotNull(normalBox);

        // The normal element should be positioned at the top of the container,
        // not pushed down by the absolute element (which is out of flow)
        // Its Y should be the same as the container's content Y
        var container = FindBoxByClass(root, "container");
        Assert.NotNull(container);
        Assert.Equal(container!.Dimensions.Y, normalBox!.Dimensions.Y, 0.1f);
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
