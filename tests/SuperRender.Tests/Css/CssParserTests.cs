using SuperRender.Core.Css;
using Xunit;

namespace SuperRender.Tests.Css;

public class CssParserTests
{
    [Fact]
    public void SimpleRule_Parsed()
    {
        var stylesheet = new CssParser("div { color: red; }").Parse();
        Assert.Single(stylesheet.Rules);
        var rule = stylesheet.Rules[0];
        Assert.Single(rule.Selectors);
        Assert.Single(rule.Declarations);
        Assert.Equal("color", rule.Declarations[0].Property);
    }

    [Fact]
    public void ClassSelector_MultipleDeclarations()
    {
        var stylesheet = new CssParser(".cls { margin: 10px; padding: 5px; }").Parse();
        Assert.Single(stylesheet.Rules);
        // Margin shorthand may expand to 4 declarations
        Assert.True(stylesheet.Rules[0].Declarations.Count >= 2);
    }

    [Fact]
    public void IdSelector_LengthValue()
    {
        var stylesheet = new CssParser("#id { width: 100px; }").Parse();
        var decl = stylesheet.Rules[0].Declarations.First(d => d.Property == "width");
        Assert.Equal(CssValueType.Length, decl.Value.Type);
        Assert.Equal(100, decl.Value.NumericValue);
        Assert.Equal("px", decl.Value.Unit);
    }

    [Fact]
    public void DescendantCombinator()
    {
        var stylesheet = new CssParser("div p { color: blue; }").Parse();
        var selector = stylesheet.Rules[0].Selectors[0];
        Assert.True(selector.Components.Count >= 2);
    }

    [Fact]
    public void CommaSeparatedSelectors()
    {
        var stylesheet = new CssParser("div, p { color: red; }").Parse();
        Assert.Equal(2, stylesheet.Rules[0].Selectors.Count);
    }

    [Fact]
    public void ColorHex_Parsed()
    {
        var stylesheet = new CssParser("div { color: #ff0000; }").Parse();
        var decl = stylesheet.Rules[0].Declarations[0];
        Assert.Equal(CssValueType.Color, decl.Value.Type);
        Assert.NotNull(decl.Value.ColorValue);
    }

    [Fact]
    public void MultipleRules()
    {
        var stylesheet = new CssParser("h1 { font-size: 32px; } p { color: gray; }").Parse();
        Assert.Equal(2, stylesheet.Rules.Count);
    }
}
