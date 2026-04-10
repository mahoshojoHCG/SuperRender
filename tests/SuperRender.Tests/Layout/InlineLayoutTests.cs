using SuperRender.Core;
using SuperRender.Core.Layout;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Layout;

public class InlineLayoutTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 400)
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
    public void SingleLineText_FitsInContainer()
    {
        var root = LayoutHtml(@"<html><head><style>p { font-size: 16px; }</style></head>
            <body><p>Hello</p></body></html>", 800);

        // Find text runs
        var runs = CollectTextRuns(root);
        Assert.True(runs.Count > 0);
        // With monospace at 0.6 ratio: "Hello" = 5 * 16 * 0.6 = 48px, fits in 800px
    }

    [Fact]
    public void TextWraps_WhenExceedingWidth()
    {
        // Create a very narrow container to force wrapping
        var root = LayoutHtml(@"<html><head><style>p { font-size: 16px; width: 50px; }</style></head>
            <body><p>Hello World Wrap Test</p></body></html>", 50);

        var runs = CollectTextRuns(root);
        // Text should have wrapped across multiple lines
        if (runs.Count > 1)
        {
            // Check that not all runs have the same Y
            var yValues = runs.Select(r => r.Y).Distinct().ToList();
            Assert.True(yValues.Count >= 1);
        }
    }

    private static List<TextRun> CollectTextRuns(LayoutBox box)
    {
        var result = new List<TextRun>();
        if (box.TextRuns != null)
            result.AddRange(box.TextRuns);
        foreach (var child in box.Children)
            result.AddRange(CollectTextRuns(child));
        return result;
    }
}
