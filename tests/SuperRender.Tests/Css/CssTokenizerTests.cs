using SuperRender.Core.Css;
using Xunit;

namespace SuperRender.Tests.Css;

public class CssTokenizerTests
{
    private static List<CssToken> Tokenize(string css)
    {
        return new CssTokenizer(css).Tokenize().ToList();
    }

    [Fact]
    public void SimpleRule_TokenTypes()
    {
        var tokens = Tokenize("div { color: red; }");
        var nonWhitespace = tokens.Where(t => t.Type != CssTokenType.Whitespace).ToList();
        Assert.Equal(CssTokenType.Ident, nonWhitespace[0].Type);
        Assert.Equal("div", nonWhitespace[0].Value);
        Assert.Equal(CssTokenType.LeftBrace, nonWhitespace[1].Type);
        Assert.Equal(CssTokenType.Ident, nonWhitespace[2].Type);
        Assert.Equal("color", nonWhitespace[2].Value);
        Assert.Equal(CssTokenType.Colon, nonWhitespace[3].Type);
        Assert.Equal(CssTokenType.Ident, nonWhitespace[4].Type);
        Assert.Equal("red", nonWhitespace[4].Value);
        Assert.Equal(CssTokenType.Semicolon, nonWhitespace[5].Type);
        Assert.Equal(CssTokenType.RightBrace, nonWhitespace[6].Type);
    }

    [Fact]
    public void HashAndDotSelectors()
    {
        var tokens = Tokenize("#header .nav");
        var nonWhitespace = tokens.Where(t => t.Type != CssTokenType.Whitespace && t.Type != CssTokenType.EndOfFile).ToList();
        Assert.Equal(CssTokenType.Hash, nonWhitespace[0].Type);
        Assert.Equal("header", nonWhitespace[0].Value);
        Assert.Equal(CssTokenType.Dot, nonWhitespace[1].Type);
        Assert.Equal(CssTokenType.Ident, nonWhitespace[2].Type);
        Assert.Equal("nav", nonWhitespace[2].Value);
    }

    [Fact]
    public void Dimension_Parsed()
    {
        var tokens = Tokenize("16px");
        Assert.Equal(CssTokenType.Dimension, tokens[0].Type);
        Assert.Equal(16, tokens[0].NumericValue);
        Assert.Equal("px", tokens[0].Unit);
    }

    [Fact]
    public void Percentage_Parsed()
    {
        var tokens = Tokenize("50%");
        Assert.Equal(CssTokenType.Percentage, tokens[0].Type);
        Assert.Equal(50, tokens[0].NumericValue);
    }

    [Fact]
    public void String_Parsed()
    {
        var tokens = Tokenize("\"Arial\"");
        Assert.Equal(CssTokenType.String, tokens[0].Type);
        Assert.Equal("Arial", tokens[0].Value);
    }

    [Fact]
    public void Comment_Skipped()
    {
        var tokens = Tokenize("/* comment */ div");
        var nonWhitespace = tokens.Where(t => t.Type != CssTokenType.Whitespace && t.Type != CssTokenType.EndOfFile).ToList();
        Assert.Single(nonWhitespace);
        Assert.Equal("div", nonWhitespace[0].Value);
    }
}
