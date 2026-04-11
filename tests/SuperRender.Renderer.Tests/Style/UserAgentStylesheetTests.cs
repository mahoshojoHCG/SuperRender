using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Tests.Style;

public class UserAgentStylesheetTests
{
    private static Dictionary<Node, ComputedStyle> ResolveWithUA(string html)
    {
        var parser = new SuperRender.Document.Html.HtmlParser(html);
        var doc = parser.Parse();
        var ua = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, ua);
        return resolver.ResolveAll(doc);
    }

    private static ComputedStyle ResolveElement(string html, string tagName)
    {
        var styles = ResolveWithUA(html);
        var element = styles.Keys.OfType<Element>().First(e => e.TagName == tagName);
        return styles[element];
    }

    // === Bold ===

    [Fact]
    public void Strong_IsBold()
    {
        var style = ResolveElement("<strong>text</strong>", "strong");
        Assert.Equal(700, style.FontWeight);
    }

    [Fact]
    public void B_IsBold()
    {
        var style = ResolveElement("<b>text</b>", "b");
        Assert.Equal(700, style.FontWeight);
    }

    [Fact]
    public void H1_IsBold()
    {
        var style = ResolveElement("<h1>text</h1>", "h1");
        Assert.Equal(700, style.FontWeight);
    }

    [Fact]
    public void H3_IsBold()
    {
        var style = ResolveElement("<h3>text</h3>", "h3");
        Assert.Equal(700, style.FontWeight);
    }

    // === Italic ===

    [Fact]
    public void Em_IsItalic()
    {
        var style = ResolveElement("<em>text</em>", "em");
        Assert.Equal(FontStyleType.Italic, style.FontStyle);
    }

    [Fact]
    public void I_IsItalic()
    {
        var style = ResolveElement("<i>text</i>", "i");
        Assert.Equal(FontStyleType.Italic, style.FontStyle);
    }

    [Fact]
    public void Cite_IsItalic()
    {
        var style = ResolveElement("<cite>text</cite>", "cite");
        Assert.Equal(FontStyleType.Italic, style.FontStyle);
    }

    [Fact]
    public void Var_IsItalic()
    {
        var style = ResolveElement("<var>text</var>", "var");
        Assert.Equal(FontStyleType.Italic, style.FontStyle);
    }

    [Fact]
    public void Address_IsItalic()
    {
        var style = ResolveElement("<address>text</address>", "address");
        Assert.Equal(FontStyleType.Italic, style.FontStyle);
    }

    // === Underline ===

    [Fact]
    public void U_HasUnderline()
    {
        var style = ResolveElement("<u>text</u>", "u");
        Assert.True(style.TextDecorationLine.HasFlag(TextDecorationLine.Underline));
    }

    [Fact]
    public void Ins_HasUnderline()
    {
        var style = ResolveElement("<ins>text</ins>", "ins");
        Assert.True(style.TextDecorationLine.HasFlag(TextDecorationLine.Underline));
    }

    [Fact]
    public void A_HasUnderline()
    {
        var style = ResolveElement("<a href=\"#\">text</a>", "a");
        Assert.True(style.TextDecorationLine.HasFlag(TextDecorationLine.Underline));
    }

    [Fact]
    public void A_HasBlueColor()
    {
        var style = ResolveElement("<a href=\"#\">text</a>", "a");
        Assert.Equal(Color.FromHex("#0000EE"), style.Color);
    }

    // === Strikethrough ===

    [Fact]
    public void S_HasLineThrough()
    {
        var style = ResolveElement("<s>text</s>", "s");
        Assert.True(style.TextDecorationLine.HasFlag(TextDecorationLine.LineThrough));
    }

    [Fact]
    public void Del_HasLineThrough()
    {
        var style = ResolveElement("<del>text</del>", "del");
        Assert.True(style.TextDecorationLine.HasFlag(TextDecorationLine.LineThrough));
    }

    // === Monospace ===

    [Fact]
    public void Kbd_IsMonospace()
    {
        var style = ResolveElement("<kbd>text</kbd>", "kbd");
        Assert.Equal("monospace", style.FontFamily);
    }

    [Fact]
    public void Samp_IsMonospace()
    {
        var style = ResolveElement("<samp>text</samp>", "samp");
        Assert.Equal("monospace", style.FontFamily);
    }

    [Fact]
    public void Code_IsMonospace()
    {
        var style = ResolveElement("<code>text</code>", "code");
        Assert.Equal("monospace", style.FontFamily);
    }

    [Fact]
    public void Pre_IsMonospace()
    {
        var style = ResolveElement("<pre>text</pre>", "pre");
        Assert.Equal("monospace", style.FontFamily);
    }

    // === Inheritance ===

    [Fact]
    public void FontWeight_Inherits_ToChild()
    {
        var styles = ResolveWithUA("<strong><span>text</span></strong>");
        var span = styles.Keys.OfType<Element>().First(e => e.TagName == "span");
        Assert.Equal(700, styles[span].FontWeight);
    }

    [Fact]
    public void FontStyle_Inherits_ToChild()
    {
        var styles = ResolveWithUA("<em><span>text</span></em>");
        var span = styles.Keys.OfType<Element>().First(e => e.TagName == "span");
        Assert.Equal(FontStyleType.Italic, styles[span].FontStyle);
    }

    [Fact]
    public void TextDecoration_DoesNotInherit()
    {
        var styles = ResolveWithUA("<u><span>text</span></u>");
        var span = styles.Keys.OfType<Element>().First(e => e.TagName == "span");
        Assert.Equal(TextDecorationLine.None, styles[span].TextDecorationLine);
    }

    // === CSS override ===

    [Fact]
    public void AuthorCSS_Overrides_UA_FontWeight()
    {
        var parser = new SuperRender.Document.Html.HtmlParser("<strong>text</strong>");
        var doc = parser.Parse();
        doc.Stylesheets.Add(new CssParser("strong { font-weight: normal; }").Parse());
        var ua = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, ua);
        var styles = resolver.ResolveAll(doc);
        var strong = styles.Keys.OfType<Element>().First(e => e.TagName == "strong");
        Assert.Equal(400, styles[strong].FontWeight);
    }

    [Fact]
    public void AuthorCSS_Overrides_UA_TextDecoration()
    {
        var parser = new SuperRender.Document.Html.HtmlParser("<a href=\"#\">text</a>");
        var doc = parser.Parse();
        doc.Stylesheets.Add(new CssParser("a { text-decoration: none; }").Parse());
        var ua = UserAgentStylesheet.Create();
        var resolver = new StyleResolver(doc.Stylesheets, ua);
        var styles = resolver.ResolveAll(doc);
        var a = styles.Keys.OfType<Element>().First(e => e.TagName == "a");
        Assert.Equal(TextDecorationLine.None, styles[a].TextDecorationLine);
    }
}
