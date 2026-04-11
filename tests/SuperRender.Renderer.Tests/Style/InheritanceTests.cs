using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Document.Html;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class InheritanceTests
{
    private static Dictionary<Node, ComputedStyle> ResolveHtml(string html)
    {
        var parser = new HtmlParser(html);
        var doc = parser.Parse();
        var stylesheets = new List<Stylesheet>();
        foreach (var child in doc.DocumentElement?.Children ?? [])
        {
            if (child is Element el && el.TagName == "head")
            {
                foreach (var hc in el.Children)
                {
                    if (hc is Element styleEl && styleEl.TagName == "style")
                    {
                        var cssText = string.Concat(styleEl.Children.OfType<TextNode>().Select(t => t.Data));
                        stylesheets.Add(new CssParser(cssText).Parse());
                    }
                }
            }
        }
        var ua = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(stylesheets, ua);
        return resolver.ResolveAll(doc);
    }

    private static ComputedStyle FindStyle(Dictionary<Node, ComputedStyle> styles, string tagName)
    {
        return styles.First(kvp => kvp.Key is Element el && el.TagName == tagName).Value;
    }

    // --- Visibility ---

    [Fact]
    public void Visibility_Default_IsVisible()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(VisibilityType.Visible, style.Visibility);
    }

    [Fact]
    public void Visibility_Hidden_AppliedCorrectly()
    {
        var styles = ResolveHtml("<html><head><style>div { visibility: hidden; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(VisibilityType.Hidden, style.Visibility);
    }

    [Fact]
    public void Visibility_Collapse_AppliedCorrectly()
    {
        var styles = ResolveHtml("<html><head><style>div { visibility: collapse; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(VisibilityType.Collapse, style.Visibility);
    }

    [Fact]
    public void Visibility_Inherited_FromParent()
    {
        var styles = ResolveHtml("<html><head><style>div { visibility: hidden; }</style></head><body><div><span>test</span></div></body></html>");
        var style = FindStyle(styles, "span");
        Assert.Equal(VisibilityType.Hidden, style.Visibility);
    }

    // --- TextTransform ---

    [Fact]
    public void TextTransform_Default_IsNone()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(TextTransformType.None, style.TextTransform);
    }

    [Fact]
    public void TextTransform_Uppercase_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { text-transform: uppercase; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(TextTransformType.Uppercase, style.TextTransform);
    }

    [Fact]
    public void TextTransform_Lowercase_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { text-transform: lowercase; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(TextTransformType.Lowercase, style.TextTransform);
    }

    [Fact]
    public void TextTransform_Capitalize_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { text-transform: capitalize; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(TextTransformType.Capitalize, style.TextTransform);
    }

    [Fact]
    public void TextTransform_Inherited_FromParent()
    {
        var styles = ResolveHtml("<html><head><style>div { text-transform: uppercase; }</style></head><body><div><span>test</span></div></body></html>");
        var style = FindStyle(styles, "span");
        Assert.Equal(TextTransformType.Uppercase, style.TextTransform);
    }

    // --- LetterSpacing ---

    [Fact]
    public void LetterSpacing_Default_IsZero()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(0f, style.LetterSpacing);
    }

    [Fact]
    public void LetterSpacing_Pixel_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { letter-spacing: 2px; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(2f, style.LetterSpacing);
    }

    [Fact]
    public void LetterSpacing_Normal_IsZero()
    {
        var styles = ResolveHtml("<html><head><style>div { letter-spacing: normal; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(0f, style.LetterSpacing);
    }

    [Fact]
    public void LetterSpacing_Inherited_FromParent()
    {
        var styles = ResolveHtml("<html><head><style>div { letter-spacing: 3px; }</style></head><body><div><span>test</span></div></body></html>");
        var style = FindStyle(styles, "span");
        Assert.Equal(3f, style.LetterSpacing);
    }

    // --- WordSpacing ---

    [Fact]
    public void WordSpacing_Default_IsZero()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(0f, style.WordSpacing);
    }

    [Fact]
    public void WordSpacing_Pixel_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { word-spacing: 5px; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(5f, style.WordSpacing);
    }

    // --- Cursor ---

    [Fact]
    public void Cursor_Default_IsAuto()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(CursorType.Auto, style.Cursor);
    }

    [Fact]
    public void Cursor_Pointer_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { cursor: pointer; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(CursorType.Pointer, style.Cursor);
    }

    [Fact]
    public void Cursor_Inherited_FromParent()
    {
        var styles = ResolveHtml("<html><head><style>div { cursor: pointer; }</style></head><body><div><span>test</span></div></body></html>");
        var style = FindStyle(styles, "span");
        Assert.Equal(CursorType.Pointer, style.Cursor);
    }

    // --- WordBreak ---

    [Fact]
    public void WordBreak_Default_IsNormal()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(WordBreakType.Normal, style.WordBreak);
    }

    [Fact]
    public void WordBreak_BreakAll_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { word-break: break-all; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(WordBreakType.BreakAll, style.WordBreak);
    }

    // --- OverflowWrap ---

    [Fact]
    public void OverflowWrap_Default_IsNormal()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(OverflowWrapType.Normal, style.OverflowWrap);
    }

    [Fact]
    public void OverflowWrap_BreakWord_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { overflow-wrap: break-word; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(OverflowWrapType.BreakWord, style.OverflowWrap);
    }

    [Fact]
    public void WordWrap_Alias_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { word-wrap: break-word; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(OverflowWrapType.BreakWord, style.OverflowWrap);
    }

    // --- ListStyleType ---

    [Fact]
    public void ListStyleType_Default_IsDisc()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal("disc", style.ListStyleType);
    }

    [Fact]
    public void ListStyleType_Decimal_Applied()
    {
        var styles = ResolveHtml("<html><head><style>ol { list-style-type: decimal; }</style></head><body><ol>test</ol></body></html>");
        var style = FindStyle(styles, "ol");
        Assert.Equal("decimal", style.ListStyleType);
    }
}

public class OpacityVisibilityTests
{
    private static Dictionary<Node, ComputedStyle> ResolveHtml(string html)
    {
        var parser = new HtmlParser(html);
        var doc = parser.Parse();
        var stylesheets = new List<Stylesheet>();
        foreach (var child in doc.DocumentElement?.Children ?? [])
        {
            if (child is Element el && el.TagName == "head")
            {
                foreach (var hc in el.Children)
                {
                    if (hc is Element styleEl && styleEl.TagName == "style")
                    {
                        var cssText = string.Concat(styleEl.Children.OfType<TextNode>().Select(t => t.Data));
                        stylesheets.Add(new CssParser(cssText).Parse());
                    }
                }
            }
        }
        var ua = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(stylesheets, ua);
        return resolver.ResolveAll(doc);
    }

    private static ComputedStyle FindStyle(Dictionary<Node, ComputedStyle> styles, string tagName)
    {
        return styles.First(kvp => kvp.Key is Element el && el.TagName == tagName).Value;
    }

    [Fact]
    public void Opacity_Default_IsOne()
    {
        var styles = ResolveHtml("<html><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(1f, style.Opacity);
    }

    [Fact]
    public void Opacity_Half_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { opacity: 0.5; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(0.5f, style.Opacity);
    }

    [Fact]
    public void Opacity_Zero_Applied()
    {
        var styles = ResolveHtml("<html><head><style>div { opacity: 0; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(0f, style.Opacity);
    }

    [Fact]
    public void Opacity_ClampsAboveOne()
    {
        var styles = ResolveHtml("<html><head><style>div { opacity: 1.5; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(1f, style.Opacity);
    }

    [Fact]
    public void Opacity_ClampsBelowZero()
    {
        var styles = ResolveHtml("<html><head><style>div { opacity: -0.5; }</style></head><body><div>test</div></body></html>");
        var style = FindStyle(styles, "div");
        Assert.Equal(0f, style.Opacity);
    }

    [Fact]
    public void Opacity_NotInherited()
    {
        // Opacity is NOT inherited per spec
        var styles = ResolveHtml("<html><head><style>div { opacity: 0.5; }</style></head><body><div><span>test</span></div></body></html>");
        var style = FindStyle(styles, "span");
        Assert.Equal(1f, style.Opacity);
    }
}
