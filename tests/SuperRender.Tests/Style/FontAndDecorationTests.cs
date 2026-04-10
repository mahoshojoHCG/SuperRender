using SuperRender.Core;
using SuperRender.Core.Css;
using SuperRender.Core.Dom;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Style;

public class FontAndDecorationTests
{
    [Theory]
    [InlineData("normal", 400f)]
    [InlineData("bold", 700f)]
    [InlineData("100", 100f)]
    [InlineData("900", 900f)]
    public void FontWeight_ParsedCorrectly(string cssValue, float expected)
    {
        var (doc, target) = CreateDoc($"div {{ font-weight: {cssValue}; }}", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(expected, style.FontWeight, 0.1f);
    }

    [Fact]
    public void FontWeight_Inherited()
    {
        var (doc, _) = CreateDoc("div { font-weight: bold; }", "<div><span>test</span></div>");
        var div = doc.Body!.Children.OfType<Element>().First();
        var span = div.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        Assert.Equal(700f, styles[span].FontWeight, 0.1f);
    }

    [Theory]
    [InlineData("normal", FontStyleType.Normal)]
    [InlineData("italic", FontStyleType.Italic)]
    [InlineData("oblique", FontStyleType.Italic)]
    public void FontStyle_ParsedCorrectly(string cssValue, FontStyleType expected)
    {
        var (doc, target) = CreateDoc($"div {{ font-style: {cssValue}; }}", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(expected, style.FontStyle);
    }

    [Fact]
    public void FontStyle_Inherited()
    {
        var (doc, _) = CreateDoc("div { font-style: italic; }", "<div><span>test</span></div>");
        var div = doc.Body!.Children.OfType<Element>().First();
        var span = div.Children.OfType<Element>().First();
        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        Assert.Equal(FontStyleType.Italic, styles[span].FontStyle);
    }

    [Fact]
    public void TextDecoration_Underline()
    {
        var (doc, target) = CreateDoc("div { text-decoration: underline; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.True(style.TextDecoration.HasFlag(TextDecorationLine.Underline));
    }

    [Fact]
    public void TextDecoration_LineThrough()
    {
        var (doc, target) = CreateDoc("div { text-decoration: line-through; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.True(style.TextDecoration.HasFlag(TextDecorationLine.LineThrough));
    }

    [Fact]
    public void TextDecoration_None()
    {
        var (doc, target) = CreateDoc("div { text-decoration: none; }", "<div>test</div>");
        var resolver = new StyleResolver(doc.Stylesheets);
        var style = resolver.Resolve(target);
        Assert.Equal(TextDecorationLine.None, style.TextDecoration);
    }

    [Fact]
    public void UserAgent_StrongIsBold()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer, useUserAgentStylesheet: true);
        var doc = pipeline.LoadHtml("<html><body><strong>bold text</strong></body></html>");

        var uaStylesheet = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, uaStylesheet);
        var styles = resolver.ResolveAll(doc);

        var strong = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal(700f, styles[strong].FontWeight, 0.1f);
    }

    [Fact]
    public void UserAgent_EmIsItalic()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer, useUserAgentStylesheet: true);
        var doc = pipeline.LoadHtml("<html><body><em>italic text</em></body></html>");

        var uaStylesheet = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, uaStylesheet);
        var styles = resolver.ResolveAll(doc);

        var em = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal(FontStyleType.Italic, styles[em].FontStyle);
    }

    [Fact]
    public void UserAgent_LinkHasUnderline()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer, useUserAgentStylesheet: true);
        var doc = pipeline.LoadHtml("<html><body><a href=\"#\">link</a></body></html>");

        var uaStylesheet = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, uaStylesheet);
        var styles = resolver.ResolveAll(doc);

        var a = doc.Body!.Children.OfType<Element>().First();
        Assert.True(styles[a].TextDecoration.HasFlag(TextDecorationLine.Underline));
    }

    [Fact]
    public void UserAgent_DelHasLineThrough()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer, useUserAgentStylesheet: true);
        var doc = pipeline.LoadHtml("<html><body><del>deleted</del></body></html>");

        var uaStylesheet = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, uaStylesheet);
        var styles = resolver.ResolveAll(doc);

        var del = doc.Body!.Children.OfType<Element>().First();
        Assert.True(styles[del].TextDecoration.HasFlag(TextDecorationLine.LineThrough));
    }

    [Fact]
    public void UserAgent_HeadingsAreBold()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer, useUserAgentStylesheet: true);
        var doc = pipeline.LoadHtml("<html><body><h1>Heading</h1></body></html>");

        var uaStylesheet = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, uaStylesheet);
        var styles = resolver.ResolveAll(doc);

        var h1 = doc.Body!.Children.OfType<Element>().First();
        Assert.Equal(700f, styles[h1].FontWeight, 0.1f);
    }

    [Fact]
    public void PaintTextRuns_IncludesFontWeightAndStyle()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(@"<html><head><style>
            div { font-weight: bold; font-style: italic; }
        </style></head><body><div>bold italic</div></body></html>");

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var engine = new LayoutEngine(measurer);
        var root = engine.BuildLayoutTree(doc, styles, 800, 600);
        var paintList = Painter.Paint(root);

        var textCmd = paintList.Commands.OfType<DrawTextCommand>().First();
        Assert.Equal(700f, textCmd.FontWeight, 0.1f);
        Assert.Equal(FontStyleType.Italic, textCmd.FontStyle);
    }

    [Fact]
    public void PaintTextRuns_UnderlineCreatesRect()
    {
        var measurer = new MonospaceTextMeasurer();
        var pipeline = new RenderPipeline(measurer);
        var doc = pipeline.LoadHtml(@"<html><head><style>
            div { text-decoration: underline; color: blue; }
        </style></head><body><div>underlined</div></body></html>");

        var resolver = new StyleResolver(doc.Stylesheets);
        var styles = resolver.ResolveAll(doc);
        var engine = new LayoutEngine(measurer);
        var root = engine.BuildLayoutTree(doc, styles, 800, 600);
        var paintList = Painter.Paint(root);

        // Should have at least one FillRect for the underline decoration
        var textCmds = paintList.Commands.OfType<DrawTextCommand>().ToList();
        Assert.True(textCmds.Count > 0);
        Assert.True(textCmds[0].TextDecoration.HasFlag(TextDecorationLine.Underline));

        // Check that FillRects exist after text commands (decoration lines)
        var fillRects = paintList.Commands.OfType<FillRectCommand>().ToList();
        Assert.True(fillRects.Count > 0);
    }

    [Fact]
    public void Clone_PreservesNewProperties()
    {
        var style = new ComputedStyle
        {
            BoxSizing = BoxSizing.BorderBox,
            MinWidth = 50f,
            MaxWidth = 500f,
            MinHeight = 20f,
            MaxHeight = 200f,
            FontWeight = 700f,
            FontStyle = FontStyleType.Italic,
            TextDecoration = TextDecorationLine.Underline,
            ZIndex = 3,
            Overflow = OverflowType.Hidden,
            TextOverflow = TextOverflowType.Ellipsis,
        };

        var clone = style.Clone();
        Assert.Equal(BoxSizing.BorderBox, clone.BoxSizing);
        Assert.Equal(50f, clone.MinWidth, 0.1f);
        Assert.Equal(500f, clone.MaxWidth, 0.1f);
        Assert.Equal(20f, clone.MinHeight, 0.1f);
        Assert.Equal(200f, clone.MaxHeight, 0.1f);
        Assert.Equal(700f, clone.FontWeight, 0.1f);
        Assert.Equal(FontStyleType.Italic, clone.FontStyle);
        Assert.Equal(TextDecorationLine.Underline, clone.TextDecoration);
        Assert.Equal(3, clone.ZIndex);
        Assert.Equal(OverflowType.Hidden, clone.Overflow);
        Assert.Equal(TextOverflowType.Ellipsis, clone.TextOverflow);
    }

    private static (Document doc, Element target) CreateDoc(string css, string bodyHtml)
    {
        var doc = new Document();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        if (!string.IsNullOrEmpty(css))
        {
            var stylesheet = new CssParser(css).Parse();
            doc.Stylesheets.Add(stylesheet);
        }

        var parser = new SuperRender.Core.Html.HtmlParser(bodyHtml);
        var parsedDoc = parser.Parse();
        if (parsedDoc.Body != null)
        {
            foreach (var child in parsedDoc.Body.Children.ToList())
            {
                child.Parent?.RemoveChild(child);
                body.AppendChild(child);
            }
        }

        var target = body.Children.OfType<Element>().FirstOrDefault()!;
        return (doc, target);
    }
}
