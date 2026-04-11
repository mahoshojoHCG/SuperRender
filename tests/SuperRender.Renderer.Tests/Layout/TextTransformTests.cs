using SuperRender.Renderer.Rendering;
using SuperRender.Renderer.Rendering.Layout;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class TextTransformTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800)
    {
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer(), useUserAgentStylesheet: true);
        pipeline.LoadHtml(html);
        pipeline.Render(viewportWidth, 600);
        return pipeline.LayoutRoot!;
    }

    private static List<TextRun> CollectTextRuns(LayoutBox box)
    {
        var runs = new List<TextRun>();
        if (box.TextRuns != null) runs.AddRange(box.TextRuns);
        foreach (var child in box.Children)
            runs.AddRange(CollectTextRuns(child));
        return runs;
    }

    [Fact]
    public void Uppercase_TransformsText()
    {
        var root = LayoutHtml("<html><head><style>div { text-transform: uppercase; }</style></head><body><div>hello world</div></body></html>");
        var runs = CollectTextRuns(root);
        var textRuns = runs.Where(r => r.Text.Trim().Length > 0).ToList();
        Assert.Contains(textRuns, r => r.Text == "HELLO");
        Assert.Contains(textRuns, r => r.Text == "WORLD");
    }

    [Fact]
    public void Lowercase_TransformsText()
    {
        var root = LayoutHtml("<html><head><style>div { text-transform: lowercase; }</style></head><body><div>HELLO WORLD</div></body></html>");
        var runs = CollectTextRuns(root);
        var textRuns = runs.Where(r => r.Text.Trim().Length > 0).ToList();
        Assert.Contains(textRuns, r => r.Text == "hello");
        Assert.Contains(textRuns, r => r.Text == "world");
    }

    [Fact]
    public void Capitalize_TransformsFirstLetters()
    {
        var root = LayoutHtml("<html><head><style>div { text-transform: capitalize; }</style></head><body><div>hello world</div></body></html>");
        var runs = CollectTextRuns(root);
        var textRuns = runs.Where(r => r.Text.Trim().Length > 0).ToList();
        Assert.Contains(textRuns, r => r.Text == "Hello");
        Assert.Contains(textRuns, r => r.Text == "World");
    }

    [Fact]
    public void None_DoesNotTransform()
    {
        var root = LayoutHtml("<html><head><style>div { text-transform: none; }</style></head><body><div>Hello World</div></body></html>");
        var runs = CollectTextRuns(root);
        var textRuns = runs.Where(r => r.Text.Trim().Length > 0).ToList();
        Assert.Contains(textRuns, r => r.Text == "Hello");
        Assert.Contains(textRuns, r => r.Text == "World");
    }

    [Fact]
    public void LetterSpacing_IncreasesWidth()
    {
        var rootNormal = LayoutHtml("<html><body><div>hello</div></body></html>");
        var rootSpaced = LayoutHtml("<html><head><style>div { letter-spacing: 5px; }</style></head><body><div>hello</div></body></html>");

        var normalRuns = CollectTextRuns(rootNormal).Where(r => r.Text == "hello").ToList();
        var spacedRuns = CollectTextRuns(rootSpaced).Where(r => r.Text == "hello").ToList();

        Assert.NotEmpty(normalRuns);
        Assert.NotEmpty(spacedRuns);
        // 5px * 4 gaps = 20px wider
        Assert.True(spacedRuns[0].Width > normalRuns[0].Width);
        Assert.Equal(normalRuns[0].Width + 20, spacedRuns[0].Width, 1f);
    }

    [Fact]
    public void WordSpacing_IncreasesSpaceWidth()
    {
        var rootNormal = LayoutHtml("<html><body><div>hello world</div></body></html>");
        var rootSpaced = LayoutHtml("<html><head><style>div { word-spacing: 10px; }</style></head><body><div>hello world</div></body></html>");

        var normalRuns = CollectTextRuns(rootNormal).Where(r => r.Text == " ").ToList();
        var spacedRuns = CollectTextRuns(rootSpaced).Where(r => r.Text == " ").ToList();

        Assert.NotEmpty(normalRuns);
        Assert.NotEmpty(spacedRuns);
        Assert.True(spacedRuns[0].Width > normalRuns[0].Width);
    }
}
