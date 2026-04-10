using SuperRender.Core.Css;
using SuperRender.Core.Dom;
using SuperRender.Core.Html;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;
using SuperRender.Core.Style;

namespace SuperRender.Core;

public sealed class RenderPipeline
{
    private readonly ITextMeasurer _textMeasurer;
    private Document? _document;
    private Dictionary<Node, ComputedStyle> _styles = new();
    private LayoutBox? _layoutRoot;
    private PaintList? _paintList;
    private float _lastWidth;
    private float _lastHeight;

    public RenderPipeline(ITextMeasurer textMeasurer)
    {
        _textMeasurer = textMeasurer;
    }

    public Document? Document => _document;

    public Document LoadHtml(string html)
    {
        var parser = new HtmlParser(html);
        _document = parser.Parse();

        // Extract CSS from <style> elements
        ExtractStylesheets(_document);

        return _document;
    }

    public PaintList Render(float viewportWidth, float viewportHeight)
    {
        if (_document == null)
            return new PaintList();

        // Style resolution
        var resolver = new StyleResolver(_document.Stylesheets);
        _styles = resolver.ResolveAll(_document);

        // Layout
        var layoutEngine = new LayoutEngine(_textMeasurer);
        _layoutRoot = layoutEngine.BuildLayoutTree(_document, _styles, viewportWidth, viewportHeight);

        // Paint
        _paintList = Painter.Paint(_layoutRoot);

        _lastWidth = viewportWidth;
        _lastHeight = viewportHeight;
        _document.NeedsLayout = false;

        return _paintList;
    }

    public PaintList? RenderIfDirty(float viewportWidth, float viewportHeight)
    {
        if (_document == null) return null;

        bool sizeChanged = Math.Abs(viewportWidth - _lastWidth) > 0.1f
                        || Math.Abs(viewportHeight - _lastHeight) > 0.1f;

        if (_document.NeedsLayout || sizeChanged)
        {
            return Render(viewportWidth, viewportHeight);
        }

        return null;
    }

    private static void ExtractStylesheets(Document document)
    {
        document.Stylesheets.Clear();
        ExtractStylesFromNode(document, document);
    }

    private static void ExtractStylesFromNode(Node node, Document document)
    {
        if (node is Element element && element.TagName == "style")
        {
            var cssText = element.InnerText;
            if (!string.IsNullOrWhiteSpace(cssText))
            {
                var cssParser = new CssParser(cssText);
                var stylesheet = cssParser.Parse();
                document.Stylesheets.Add(stylesheet);
            }
        }

        foreach (var child in node.Children)
        {
            ExtractStylesFromNode(child, document);
        }
    }
}
