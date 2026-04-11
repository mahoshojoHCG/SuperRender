using SuperRender.Core;
using SuperRender.Core.Layout;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Layout;

public class WhiteSpaceTests
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
    public void WhiteSpaceNormal_CollapsesWhitespace()
    {
        // With white-space:normal, multiple spaces between words should be collapsed.
        // We verify this by checking that the text runs don't contain multiple spaces.
        var root = LayoutHtml(@"
            <html><head><style>p { white-space: normal; font-size: 16px; }</style></head>
            <body><p>hello   world</p></body></html>");

        var runs = CollectTextRuns(root);
        // No run should contain multiple consecutive spaces
        foreach (var run in runs)
        {
            Assert.DoesNotContain("  ", run.Text);
        }
    }

    [Fact]
    public void WhiteSpacePre_PreservesSpaces()
    {
        // With white-space:pre, spaces should be preserved, producing more text runs
        var root = LayoutHtml(@"
            <html><head><style>div { white-space: pre; font-size: 16px; }</style></head>
            <body><div>hello   world</div></body></html>");

        var runs = CollectTextRuns(root);
        // The total text should contain the original spaces
        var allText = string.Concat(runs.Select(r => r.Text));
        Assert.Contains("   ", allText);
    }

    [Fact]
    public void WhiteSpaceNowrap_CollapsesWhitespace()
    {
        // With white-space:nowrap, multiple spaces should be collapsed (like normal)
        var root = LayoutHtml(@"
            <html><head><style>p { white-space: nowrap; font-size: 16px; }</style></head>
            <body><p>hello   world</p></body></html>");

        var runs = CollectTextRuns(root);
        foreach (var run in runs)
        {
            Assert.DoesNotContain("  ", run.Text);
        }
    }

    [Fact]
    public void WhiteSpaceNowrap_PreventsLineBreaks()
    {
        // Create a very narrow container with nowrap - text should not wrap
        var root = LayoutHtml(@"
            <html><head><style>
                p { white-space: nowrap; width: 50px; font-size: 16px; }
            </style></head><body><p>Hello World Test Long Text</p></body></html>", 800);

        var runs = CollectTextRuns(root);

        // In nowrap mode, all text runs should be on the same Y line
        if (runs.Count > 1)
        {
            var yValues = runs.Select(r => r.Y).Distinct().ToList();
            Assert.Single(yValues);
        }
    }

    [Fact]
    public void WhiteSpacePre_PreservesNewlinesInLayout()
    {
        var root = LayoutHtml(@"
            <html><head><style>
                pre { white-space: pre; font-size: 16px; }
            </style></head><body><pre>line1
line2
line3</pre></body></html>");

        var runs = CollectTextRuns(root);

        // Should have runs on multiple lines due to newlines being preserved
        if (runs.Count > 1)
        {
            var yValues = runs.Select(r => r.Y).Distinct().ToList();
            Assert.True(yValues.Count >= 2);
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
