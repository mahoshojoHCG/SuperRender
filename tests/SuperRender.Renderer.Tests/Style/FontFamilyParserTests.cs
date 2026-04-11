using SuperRender.Document.Style;
using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class FontFamilyParserTests
{
    [Fact]
    public void Parse_SingleUnquotedFamily()
    {
        var result = FontFamilyParser.Parse("Arial");
        Assert.Single(result);
        Assert.Equal("Arial", result[0]);
    }

    [Fact]
    public void Parse_SingleDoubleQuotedFamily()
    {
        var result = FontFamilyParser.Parse("\"Helvetica Neue\"");
        Assert.Single(result);
        Assert.Equal("Helvetica Neue", result[0]);
    }

    [Fact]
    public void Parse_SingleSingleQuotedFamily()
    {
        var result = FontFamilyParser.Parse("'Georgia'");
        Assert.Single(result);
        Assert.Equal("Georgia", result[0]);
    }

    [Fact]
    public void Parse_CommaSeparatedList()
    {
        var result = FontFamilyParser.Parse("\"Helvetica Neue\", Arial, sans-serif");
        Assert.Equal(3, result.Count);
        Assert.Equal("Helvetica Neue", result[0]);
        Assert.Equal("Arial", result[1]);
        Assert.Equal("sans-serif", result[2]);
    }

    [Fact]
    public void Parse_MixedQuoting()
    {
        var result = FontFamilyParser.Parse("'Georgia', \"Times New Roman\", serif");
        Assert.Equal(3, result.Count);
        Assert.Equal("Georgia", result[0]);
        Assert.Equal("Times New Roman", result[1]);
        Assert.Equal("serif", result[2]);
    }

    [Fact]
    public void Parse_SingleGenericFamily()
    {
        var result = FontFamilyParser.Parse("monospace");
        Assert.Single(result);
        Assert.Equal("monospace", result[0]);
    }

    [Fact]
    public void Parse_WhitespaceHandling()
    {
        var result = FontFamilyParser.Parse("  Arial  ,  Helvetica  ");
        Assert.Equal(2, result.Count);
        Assert.Equal("Arial", result[0]);
        Assert.Equal("Helvetica", result[1]);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var result = FontFamilyParser.Parse("");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmpty()
    {
        var result = FontFamilyParser.Parse("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyEntriesFiltered()
    {
        var result = FontFamilyParser.Parse("Arial,,sans-serif");
        Assert.Equal(2, result.Count);
        Assert.Equal("Arial", result[0]);
        Assert.Equal("sans-serif", result[1]);
    }

    [Fact]
    public void IsGenericFamily_RecognizesGenerics()
    {
        Assert.True(FontFamilyParser.IsGenericFamily("serif"));
        Assert.True(FontFamilyParser.IsGenericFamily("sans-serif"));
        Assert.True(FontFamilyParser.IsGenericFamily("monospace"));
        Assert.True(FontFamilyParser.IsGenericFamily("cursive"));
        Assert.True(FontFamilyParser.IsGenericFamily("fantasy"));
        Assert.True(FontFamilyParser.IsGenericFamily("system-ui"));
        Assert.True(FontFamilyParser.IsGenericFamily("ui-monospace"));
    }

    [Fact]
    public void IsGenericFamily_CaseInsensitive()
    {
        Assert.True(FontFamilyParser.IsGenericFamily("SERIF"));
        Assert.True(FontFamilyParser.IsGenericFamily("Sans-Serif"));
        Assert.True(FontFamilyParser.IsGenericFamily("Monospace"));
    }

    [Fact]
    public void IsGenericFamily_RejectsNonGenerics()
    {
        Assert.False(FontFamilyParser.IsGenericFamily("Arial"));
        Assert.False(FontFamilyParser.IsGenericFamily("Helvetica"));
        Assert.False(FontFamilyParser.IsGenericFamily("Times New Roman"));
    }

    [Fact]
    public void StyleResolver_FontFamilyList_Integrated()
    {
        // Verify that font-family CSS value flows through the full pipeline
        var css = "p { font-family: \"Helvetica Neue\", Arial, sans-serif; }";
        var stylesheet = new CssParser(css).Parse();

        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var p = new Element("p");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(p);

        var resolver = new StyleResolver([stylesheet], UserAgentStylesheet.Create());
        var styles = resolver.ResolveAll(doc);
        var style = styles[p];

        Assert.Equal(3, style.FontFamilies.Count);
        Assert.Equal("Helvetica Neue", style.FontFamilies[0]);
        Assert.Equal("Arial", style.FontFamilies[1]);
        Assert.Equal("sans-serif", style.FontFamilies[2]);
        // Backward compat: FontFamily returns first entry
        Assert.Equal("Helvetica Neue", style.FontFamily);
    }

    [Fact]
    public void StyleResolver_FontFamilySingle_Integrated()
    {
        var css = "code { font-family: monospace; }";
        var stylesheet = new CssParser(css).Parse();

        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var code = new Element("code");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(code);

        var resolver = new StyleResolver([stylesheet], UserAgentStylesheet.Create());
        var styles = resolver.ResolveAll(doc);
        var style = styles[code];

        Assert.Single(style.FontFamilies);
        Assert.Equal("monospace", style.FontFamilies[0]);
        Assert.Equal("monospace", style.FontFamily);
    }

    [Fact]
    public void FontFamilies_InheritedFromParent()
    {
        var css = "body { font-family: \"Segoe UI\", Tahoma, sans-serif; }";
        var stylesheet = new CssParser(css).Parse();

        var doc = new DomDocument();
        var html = new Element("html");
        var body = new Element("body");
        var p = new Element("p");
        doc.AppendChild(html);
        html.AppendChild(body);
        body.AppendChild(p);

        var resolver = new StyleResolver([stylesheet], UserAgentStylesheet.Create());
        var styles = resolver.ResolveAll(doc);
        var style = styles[p];

        // p inherits from body
        Assert.Equal(3, style.FontFamilies.Count);
        Assert.Equal("Segoe UI", style.FontFamilies[0]);
    }
}
