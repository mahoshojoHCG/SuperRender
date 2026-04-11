using SuperRender.Renderer.Rendering;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Painting;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class OverflowTests
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
    public void OverflowHidden_GeneratesPushClipAndPopClip()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { overflow: hidden; width: 200px; height: 100px; }
            </style></head><body><div>text content here</div></body></html>");

        var paintList = Painter.Paint(root);
        var commands = paintList.Commands;

        Assert.Contains(commands, c => c is PushClipCommand);
        Assert.Contains(commands, c => c is PopClipCommand);
    }

    [Fact]
    public void OverflowHidden_WithExplicitHeight_ClampsContentHeight()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { overflow: hidden; height: 50px; }
            </style></head><body><div>text</div></body></html>");

        var div = FindBox(root, "div");
        Assert.NotNull(div);

        Assert.Equal(50f, div!.Dimensions.Height, 0.1f);
    }

    [Fact]
    public void OverflowVisible_NoPushClip()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                div { overflow: visible; width: 200px; height: 100px; }
            </style></head><body><div>text</div></body></html>");

        var paintList = Painter.Paint(root);

        // Should not generate clip commands for visible overflow
        Assert.DoesNotContain(paintList.Commands, c => c is PushClipCommand);
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
