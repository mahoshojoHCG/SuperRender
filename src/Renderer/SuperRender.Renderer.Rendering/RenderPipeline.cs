using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using SuperRender.Renderer.Rendering.Layout;
using SuperRender.Renderer.Rendering.Painting;
using SuperRender.Renderer.Rendering.Style;

namespace SuperRender.Renderer.Rendering;

public sealed class RenderPipeline
{
    private readonly ITextMeasurer _textMeasurer;
    private readonly bool _useUserAgentStylesheet;
    private readonly TransitionController _transitions = new();
    private readonly AnimationController _animations = new();
    private DomDocument? _document;
    private Dictionary<Node, ComputedStyle> _styles = new();
    private LayoutBox? _layoutRoot;
    private PaintList? _paintList;
    private float _lastWidth;
    private float _lastHeight;
    private bool _transitionsActive;
    private bool _animationsActive;

    public RenderPipeline(ITextMeasurer textMeasurer, bool useUserAgentStylesheet = false)
    {
        _textMeasurer = textMeasurer;
        _useUserAgentStylesheet = useUserAgentStylesheet;
    }

    public DomDocument? Document => _document;
    public LayoutBox? LayoutRoot => _layoutRoot;

    public DomDocument LoadHtml(string html)
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
        var uaStylesheet = _useUserAgentStylesheet ? UserAgentStylesheet.Create() : null;
        var resolver = new StyleResolver(_document.Stylesheets, uaStylesheet);
        _styles = resolver.ResolveAll(_document, viewportWidth, viewportHeight);

        // Register @keyframes from the current stylesheets (cheap — parsed once per render).
        _animations.LoadKeyframes(_document.Stylesheets);
        _animations.ViewportWidth = viewportWidth;
        _animations.ViewportHeight = viewportHeight;

        // Apply in-flight transitions (overrides transitionable properties with
        // their currently interpolated values). May start new transitions when
        // hover/focus state changes alter the target computed values.
        _transitionsActive = _transitions.Apply(_styles);

        // Advance @keyframes animations and overwrite animated fields.
        _animationsActive = _animations.Apply(_styles);

        // Layout
        var layoutEngine = new LayoutEngine(_textMeasurer);
        _layoutRoot = layoutEngine.BuildLayoutTree(_document, _styles, viewportWidth, viewportHeight, resolver.PseudoElements);

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

        if (_document.NeedsLayout || sizeChanged || _transitionsActive || _animationsActive)
        {
            return Render(viewportWidth, viewportHeight);
        }

        return null;
    }

    private static void ExtractStylesheets(DomDocument document)
    {
        document.Stylesheets.Clear();
        ExtractStylesFromNode(document, document);
    }

    private static void ExtractStylesFromNode(Node node, DomDocument document)
    {
        if (node is Element element && element.TagName == HtmlTagNames.Style)
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
