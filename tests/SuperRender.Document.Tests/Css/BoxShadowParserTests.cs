using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class BoxShadowParserTests
{
    [Fact]
    public void SimpleShadow_TwoOffsets()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px");
        Assert.Single(shadows);
        Assert.Equal(10f, shadows[0].OffsetX);
        Assert.Equal(5f, shadows[0].OffsetY);
        Assert.Equal(0f, shadows[0].BlurRadius);
        Assert.Equal(0f, shadows[0].SpreadRadius);
        Assert.False(shadows[0].Inset);
    }

    [Fact]
    public void Shadow_WithBlur()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px 15px");
        Assert.Single(shadows);
        Assert.Equal(10f, shadows[0].OffsetX);
        Assert.Equal(5f, shadows[0].OffsetY);
        Assert.Equal(15f, shadows[0].BlurRadius);
    }

    [Fact]
    public void Shadow_WithBlurAndSpread()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px 15px 3px");
        Assert.Single(shadows);
        Assert.Equal(10f, shadows[0].OffsetX);
        Assert.Equal(5f, shadows[0].OffsetY);
        Assert.Equal(15f, shadows[0].BlurRadius);
        Assert.Equal(3f, shadows[0].SpreadRadius);
    }

    [Fact]
    public void Shadow_WithColor()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px red");
        Assert.Single(shadows);
        Assert.Equal(10f, shadows[0].OffsetX);
        Assert.Equal(5f, shadows[0].OffsetY);
        // Red color
        Assert.Equal(1f, shadows[0].Color.R, 0.01f);
        Assert.Equal(0f, shadows[0].Color.G, 0.01f);
        Assert.Equal(0f, shadows[0].Color.B, 0.01f);
    }

    [Fact]
    public void Shadow_Inset()
    {
        var shadows = CssParser.ParseBoxShadowValue("inset 10px 5px 15px red");
        Assert.Single(shadows);
        Assert.True(shadows[0].Inset);
        Assert.Equal(10f, shadows[0].OffsetX);
        Assert.Equal(5f, shadows[0].OffsetY);
        Assert.Equal(15f, shadows[0].BlurRadius);
    }

    [Fact]
    public void Shadow_InsetAtEnd()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px 15px red inset");
        Assert.Single(shadows);
        Assert.True(shadows[0].Inset);
    }

    [Fact]
    public void Shadow_HexColor()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px #ff0000");
        Assert.Single(shadows);
        Assert.Equal(1f, shadows[0].Color.R, 0.01f);
    }

    [Fact]
    public void Shadow_RgbColor()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px rgb(255, 0, 0)");
        Assert.Single(shadows);
        Assert.Equal(1f, shadows[0].Color.R, 0.01f);
    }

    [Fact]
    public void MultipleShadows()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px red, 20px 10px blue");
        Assert.Equal(2, shadows.Count);
        Assert.Equal(10f, shadows[0].OffsetX);
        Assert.Equal(20f, shadows[1].OffsetX);
    }

    [Fact]
    public void None_ReturnsEmpty()
    {
        var shadows = CssParser.ParseBoxShadowValue("none");
        Assert.Empty(shadows);
    }

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        var shadows = CssParser.ParseBoxShadowValue("");
        Assert.Empty(shadows);
    }

    [Fact]
    public void Shadow_NegativeOffsets()
    {
        var shadows = CssParser.ParseBoxShadowValue("-5px -3px 10px");
        Assert.Single(shadows);
        Assert.Equal(-5f, shadows[0].OffsetX);
        Assert.Equal(-3f, shadows[0].OffsetY);
    }

    [Fact]
    public void Shadow_BlurCannotBeNegative()
    {
        var shadows = CssParser.ParseBoxShadowValue("5px 3px -10px");
        Assert.Single(shadows);
        // Blur radius is clamped to 0
        Assert.Equal(0f, shadows[0].BlurRadius);
    }

    [Fact]
    public void Shadow_NegativeSpread()
    {
        var shadows = CssParser.ParseBoxShadowValue("5px 3px 10px -2px");
        Assert.Single(shadows);
        Assert.Equal(-2f, shadows[0].SpreadRadius);
    }

    [Fact]
    public void Shadow_DefaultColor_IsBlack()
    {
        var shadows = CssParser.ParseBoxShadowValue("10px 5px");
        Assert.Single(shadows);
        Assert.Equal(0f, shadows[0].Color.R, 0.01f);
        Assert.Equal(0f, shadows[0].Color.G, 0.01f);
        Assert.Equal(0f, shadows[0].Color.B, 0.01f);
    }

    [Fact]
    public void Shadow_ZeroOffset()
    {
        var shadows = CssParser.ParseBoxShadowValue("0 0 10px black");
        Assert.Single(shadows);
        Assert.Equal(0f, shadows[0].OffsetX);
        Assert.Equal(0f, shadows[0].OffsetY);
        Assert.Equal(10f, shadows[0].BlurRadius);
    }

    [Fact]
    public void ThreeShadows()
    {
        var shadows = CssParser.ParseBoxShadowValue("1px 1px red, 2px 2px green, 3px 3px blue");
        Assert.Equal(3, shadows.Count);
    }

    [Fact]
    public void Shadow_LargeValues()
    {
        var shadows = CssParser.ParseBoxShadowValue("100px 200px 50px 25px black");
        Assert.Single(shadows);
        Assert.Equal(100f, shadows[0].OffsetX);
        Assert.Equal(200f, shadows[0].OffsetY);
        Assert.Equal(50f, shadows[0].BlurRadius);
        Assert.Equal(25f, shadows[0].SpreadRadius);
    }

    [Fact]
    public void Stylesheet_BoxShadow_Parsed()
    {
        var css = "div { box-shadow: 10px 5px 15px red; }";
        var stylesheet = new CssParser(css).Parse();
        var decl = stylesheet.Rules[0].Declarations[0];
        Assert.Equal("box-shadow", decl.Property);
    }

    [Fact]
    public void InlineStyle_BoxShadow()
    {
        var decls = CssParser.ParseInlineStyleDeclarations("box-shadow: 2px 2px 5px rgba(0, 0, 0, 0.5)");
        Assert.Single(decls);
        Assert.Equal("box-shadow", decls[0].Property);
    }
}
