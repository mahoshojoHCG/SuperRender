using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class SupportsConditionTests
{
    [Fact]
    public void DisplayFlex_IsSupported()
    {
        var sc = new SupportsCondition("(display: flex)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void DisplayBlock_IsSupported()
    {
        var sc = new SupportsCondition("(display: block)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void DisplayGrid_IsNotSupported()
    {
        var sc = new SupportsCondition("(display: grid)");
        Assert.False(sc.Evaluate());
    }

    [Fact]
    public void Color_IsSupported()
    {
        var sc = new SupportsCondition("(color: red)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void UnknownProperty_IsNotSupported()
    {
        var sc = new SupportsCondition("(container-type: inline-size)");
        Assert.False(sc.Evaluate());
    }

    [Fact]
    public void NotCondition_InvertsResult()
    {
        var sc = new SupportsCondition("not (display: grid)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void NotCondition_SupportedProperty_ReturnsFalse()
    {
        var sc = new SupportsCondition("not (display: flex)");
        Assert.False(sc.Evaluate());
    }

    [Fact]
    public void AndCondition_BothSupported()
    {
        var sc = new SupportsCondition("(display: flex) and (color: red)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void AndCondition_OneUnsupported()
    {
        var sc = new SupportsCondition("(display: flex) and (display: grid)");
        Assert.False(sc.Evaluate());
    }

    [Fact]
    public void OrCondition_OneSupported()
    {
        var sc = new SupportsCondition("(display: grid) or (display: flex)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void OrCondition_NoneSupported()
    {
        var sc = new SupportsCondition("(display: grid) or (container-type: size)");
        Assert.False(sc.Evaluate());
    }

    [Fact]
    public void EmptyCondition_ReturnsFalse()
    {
        var sc = new SupportsCondition("");
        Assert.False(sc.Evaluate());
    }

    [Fact]
    public void Padding_IsSupported()
    {
        var sc = new SupportsCondition("(padding: 10px)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void FlexDirection_IsSupported()
    {
        var sc = new SupportsCondition("(flex-direction: row)");
        Assert.True(sc.Evaluate());
    }

    [Fact]
    public void TextIndent_IsSupported()
    {
        var sc = new SupportsCondition("(text-indent: 2em)");
        Assert.True(sc.Evaluate());
    }
}
