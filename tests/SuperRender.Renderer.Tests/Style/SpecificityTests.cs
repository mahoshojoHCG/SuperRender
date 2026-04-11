using SuperRender.Document.Css;
using SuperRender.Document.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class SpecificityTests
{
    private static Specificity Calc(string selectorText)
    {
        var tokens = new CssTokenizer(selectorText).Tokenize()
            .Where(t => t.Type != CssTokenType.EndOfFile)
            .ToList();
        var selectors = new SelectorParser(tokens).ParseSelectorList();
        return selectors[0].GetSpecificity();
    }

    [Fact]
    public void Universal_ZeroSpecificity()
    {
        var s = Calc("*");
        Assert.Equal(0, s.Ids);
        Assert.Equal(0, s.Classes);
        Assert.Equal(0, s.Elements);
    }

    [Fact]
    public void TagSelector_OneElement()
    {
        var s = Calc("div");
        Assert.Equal(new Specificity { Ids = 0, Classes = 0, Elements = 1 }, s);
    }

    [Fact]
    public void ClassSelector_OneClass()
    {
        var s = Calc(".cls");
        Assert.Equal(new Specificity { Ids = 0, Classes = 1, Elements = 0 }, s);
    }

    [Fact]
    public void IdSelector_OneId()
    {
        var s = Calc("#id");
        Assert.Equal(new Specificity { Ids = 1, Classes = 0, Elements = 0 }, s);
    }

    [Fact]
    public void Compound_TagAndClass()
    {
        var s = Calc("div.cls");
        Assert.Equal(new Specificity { Ids = 0, Classes = 1, Elements = 1 }, s);
    }

    [Fact]
    public void Complex_IdClassTag()
    {
        var s = Calc("#id .cls div");
        Assert.Equal(new Specificity { Ids = 1, Classes = 1, Elements = 1 }, s);
    }

    [Fact]
    public void Comparison_IdWinsOverManyClasses()
    {
        var idSpec = new Specificity { Ids = 1, Classes = 0, Elements = 0 };
        var classSpec = new Specificity { Ids = 0, Classes = 10, Elements = 0 };
        Assert.True(idSpec > classSpec);
    }

    [Fact]
    public void Comparison_ClassWinsOverManyElements()
    {
        var classSpec = new Specificity { Ids = 0, Classes = 1, Elements = 0 };
        var elemSpec = new Specificity { Ids = 0, Classes = 0, Elements = 99 };
        Assert.True(classSpec > elemSpec);
    }
}
