using SuperRender.Core.Html;
using Xunit;

namespace SuperRender.Tests.Html;

public class HtmlTokenizerTests
{
    private static List<HtmlToken> Tokenize(string html)
    {
        var tokenizer = new HtmlTokenizer(html);
        return tokenizer.Tokenize().ToList();
    }

    [Fact]
    public void SimpleDiv_ProducesStartTextEndTokens()
    {
        var tokens = Tokenize("<div>hello</div>");
        Assert.Equal(HtmlTokenType.StartTag, tokens[0].Type);
        Assert.Equal("div", tokens[0].TagName);
        Assert.Equal(HtmlTokenType.Text, tokens[1].Type);
        Assert.Equal("hello", tokens[1].Text);
        Assert.Equal(HtmlTokenType.EndTag, tokens[2].Type);
        Assert.Equal("div", tokens[2].TagName);
        Assert.Equal(HtmlTokenType.EndOfFile, tokens[^1].Type);
    }

    [Fact]
    public void SelfClosingImg_ParsesAttributes()
    {
        var tokens = Tokenize("<img src=\"x.png\" />");
        Assert.Equal(HtmlTokenType.StartTag, tokens[0].Type);
        Assert.Equal("img", tokens[0].TagName);
        Assert.True(tokens[0].SelfClosing);
        Assert.Equal("x.png", tokens[0].Attributes["src"]);
    }

    [Fact]
    public void ClassAttribute_WithSpaces()
    {
        var tokens = Tokenize("<p class=\"a b\">");
        Assert.Equal("a b", tokens[0].Attributes["class"]);
    }

    [Fact]
    public void Comment_Parsed()
    {
        var tokens = Tokenize("<!-- comment -->text");
        Assert.Equal(HtmlTokenType.Comment, tokens[0].Type);
        Assert.Equal(HtmlTokenType.Text, tokens[1].Type);
        Assert.Equal("text", tokens[1].Text);
    }

    [Fact]
    public void EntityDecoding_InText()
    {
        var tokens = Tokenize("&amp; &lt;");
        Assert.Equal(HtmlTokenType.Text, tokens[0].Type);
        Assert.Equal("& <", tokens[0].Text);
    }

    [Fact]
    public void NestedTags()
    {
        var tokens = Tokenize("<div><span>text</span></div>");
        Assert.Equal("div", tokens[0].TagName);
        Assert.Equal("span", tokens[1].TagName);
        Assert.Equal(HtmlTokenType.Text, tokens[2].Type);
        Assert.Equal("span", tokens[3].TagName);
        Assert.Equal(HtmlTokenType.EndTag, tokens[3].Type);
        Assert.Equal("div", tokens[4].TagName);
        Assert.Equal(HtmlTokenType.EndTag, tokens[4].Type);
    }

    [Fact]
    public void TagName_NormalizedToLowercase()
    {
        var tokens = Tokenize("<DIV>");
        Assert.Equal("div", tokens[0].TagName);
    }

    [Fact]
    public void Doctype_Parsed()
    {
        var tokens = Tokenize("<!DOCTYPE html><html>");
        Assert.Equal(HtmlTokenType.Doctype, tokens[0].Type);
        Assert.Equal(HtmlTokenType.StartTag, tokens[1].Type);
    }
}
