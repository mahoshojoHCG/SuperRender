using SuperRender.Core.Css;
using Xunit;

namespace SuperRender.Tests.Css;

public class SelectorParserTests
{
    private static List<Selector> Parse(string selectorText)
    {
        var tokens = new CssTokenizer(selectorText).Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
        return new SelectorParser(tokens).ParseSelectorList();
    }

    [Fact]
    public void TagSelector()
    {
        var selectors = Parse("div");
        Assert.Single(selectors);
        Assert.Single(selectors[0].Components);
        Assert.Equal("div", selectors[0].Components[0].Simple.TagName);
    }

    [Fact]
    public void ClassSelector()
    {
        var selectors = Parse(".foo");
        Assert.Single(selectors);
        Assert.Contains("foo", selectors[0].Components[0].Simple.Classes);
    }

    [Fact]
    public void IdSelector()
    {
        var selectors = Parse("#bar");
        Assert.Single(selectors);
        Assert.Equal("bar", selectors[0].Components[0].Simple.Id);
    }

    [Fact]
    public void CompoundSelector()
    {
        var selectors = Parse("div.foo#bar");
        Assert.Single(selectors);
        var simple = selectors[0].Components[0].Simple;
        Assert.Equal("div", simple.TagName);
        Assert.Contains("foo", simple.Classes);
        Assert.Equal("bar", simple.Id);
    }

    [Fact]
    public void DescendantCombinator()
    {
        var selectors = Parse("div p");
        Assert.Single(selectors);
        Assert.Equal(2, selectors[0].Components.Count);
        Assert.Equal("div", selectors[0].Components[0].Simple.TagName);
        Assert.Equal("p", selectors[0].Components[1].Simple.TagName);
    }
}
