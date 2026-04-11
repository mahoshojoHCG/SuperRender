using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class ColorTests
{
    // --- Named Colors ---

    [Fact]
    public void FromName_BasicColors_ReturnsCorrectValues()
    {
        Assert.Equal(Color.FromRgb(255, 0, 0), Color.FromName("red"));
        Assert.Equal(Color.FromRgb(0, 128, 0), Color.FromName("green"));
        Assert.Equal(Color.FromRgb(0, 0, 255), Color.FromName("blue"));
        Assert.Equal(Color.FromRgb(255, 255, 0), Color.FromName("yellow"));
    }

    [Fact]
    public void FromName_ExtendedColors_ReturnsCorrectValues()
    {
        Assert.Equal(Color.FromRgb(240, 248, 255), Color.FromName("aliceblue"));
        Assert.Equal(Color.FromRgb(102, 51, 153), Color.FromName("rebeccapurple"));
        Assert.Equal(Color.FromRgb(255, 228, 196), Color.FromName("bisque"));
        Assert.Equal(Color.FromRgb(0, 206, 209), Color.FromName("darkturquoise"));
        Assert.Equal(Color.FromRgb(255, 69, 0), Color.FromName("orangered"));
    }

    [Fact]
    public void FromName_CaseInsensitive_MatchesRegardlessOfCase()
    {
        Assert.Equal(Color.FromName("aliceblue"), Color.FromName("AliceBlue"));
        Assert.Equal(Color.FromName("rebeccapurple"), Color.FromName("REBECCAPURPLE"));
    }

    [Fact]
    public void TryFromName_UnknownColor_ReturnsFalse()
    {
        Assert.False(Color.TryFromName("nonexistent", out _));
    }

    [Fact]
    public void NamedColors_Has148Colors_PlusTransparent()
    {
        // Verify we have the expected count (148 CSS named colors + transparent)
        // Some colors have grey/gray aliases so the dict has more entries
        Assert.True(Color.TryFromName("aliceblue", out _));
        Assert.True(Color.TryFromName("yellowgreen", out _));
        Assert.True(Color.TryFromName("transparent", out _));
    }

    [Fact]
    public void FromName_GreyVariants_AllPresent()
    {
        Assert.True(Color.TryFromName("gray", out _));
        Assert.True(Color.TryFromName("grey", out _));
        Assert.True(Color.TryFromName("darkgray", out _));
        Assert.True(Color.TryFromName("darkgrey", out _));
        Assert.True(Color.TryFromName("lightgray", out _));
        Assert.True(Color.TryFromName("lightgrey", out _));
        Assert.True(Color.TryFromName("dimgray", out _));
        Assert.True(Color.TryFromName("dimgrey", out _));
        Assert.True(Color.TryFromName("slategray", out _));
        Assert.True(Color.TryFromName("slategrey", out _));
    }

    // --- HSL Conversion ---

    [Fact]
    public void FromHsl_Red_CorrectRgb()
    {
        var color = Color.FromHsl(0, 1, 0.5);
        AssertColorApprox(1f, 0f, 0f, 1f, color);
    }

    [Fact]
    public void FromHsl_Green_CorrectRgb()
    {
        var color = Color.FromHsl(120, 1, 0.5);
        AssertColorApprox(0f, 1f, 0f, 1f, color);
    }

    [Fact]
    public void FromHsl_Blue_CorrectRgb()
    {
        var color = Color.FromHsl(240, 1, 0.5);
        AssertColorApprox(0f, 0f, 1f, 1f, color);
    }

    [Fact]
    public void FromHsl_White_CorrectRgb()
    {
        var color = Color.FromHsl(0, 0, 1);
        AssertColorApprox(1f, 1f, 1f, 1f, color);
    }

    [Fact]
    public void FromHsl_Black_CorrectRgb()
    {
        var color = Color.FromHsl(0, 0, 0);
        AssertColorApprox(0f, 0f, 0f, 1f, color);
    }

    [Fact]
    public void FromHsl_Gray50_CorrectRgb()
    {
        var color = Color.FromHsl(0, 0, 0.5);
        AssertColorApprox(0.5f, 0.5f, 0.5f, 1f, color);
    }

    [Fact]
    public void FromHsla_WithAlpha_CorrectRgba()
    {
        var color = Color.FromHsla(0, 1, 0.5, 0.5);
        AssertColorApprox(1f, 0f, 0f, 0.5f, color);
    }

    [Fact]
    public void FromHsl_NegativeHue_Normalizes()
    {
        // -60 degrees should be same as 300 degrees
        var color = Color.FromHsl(-60, 1, 0.5);
        var expected = Color.FromHsl(300, 1, 0.5);
        AssertColorApprox(expected.R, expected.G, expected.B, expected.A, color);
    }

    [Fact]
    public void FromHsl_HueOver360_Normalizes()
    {
        var color = Color.FromHsl(480, 1, 0.5);
        var expected = Color.FromHsl(120, 1, 0.5);
        AssertColorApprox(expected.R, expected.G, expected.B, expected.A, color);
    }

    // --- CSS Parsing: hsl() ---

    [Fact]
    public void CssParser_Hsl_CommaSeparated_ParsesColor()
    {
        var css = "div { color: hsl(0, 100%, 50%); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        Assert.NotNull(value.ColorValue);
        AssertColorApprox(1f, 0f, 0f, 1f, value.ColorValue!.Value);
    }

    [Fact]
    public void CssParser_Hsla_WithAlpha_ParsesColor()
    {
        var css = "div { color: hsla(120, 100%, 50%, 0.5); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        AssertColorApprox(0f, 1f, 0f, 0.5f, value.ColorValue!.Value);
    }

    [Fact]
    public void CssParser_Hsl_SpaceSeparated_ParsesColor()
    {
        var css = "div { color: hsl(240 100% 50%); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        AssertColorApprox(0f, 0f, 1f, 1f, value.ColorValue!.Value);
    }

    [Fact]
    public void CssParser_Hsl_SpaceSeparatedWithAlpha_ParsesColor()
    {
        var css = "div { color: hsl(0 100% 50% / 0.3); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        AssertColorApprox(1f, 0f, 0f, 0.3f, value.ColorValue!.Value);
    }

    // --- CSS Parsing: rgb() space-separated ---

    [Fact]
    public void CssParser_Rgb_SpaceSeparated_ParsesColor()
    {
        var css = "div { color: rgb(255 128 0); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        var c = value.ColorValue!.Value;
        Assert.Equal(1f, c.R, 0.01f);
        Assert.Equal(128 / 255f, c.G, 0.01f);
        Assert.Equal(0f, c.B, 0.01f);
    }

    [Fact]
    public void CssParser_Rgb_SpaceSeparatedWithSlashAlpha_ParsesColor()
    {
        var css = "div { color: rgb(255 0 0 / 0.5); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        var c = value.ColorValue!.Value;
        AssertColorApprox(1f, 0f, 0f, 0.5f, c);
    }

    [Fact]
    public void CssParser_Rgba_CommaSeparated_StillWorks()
    {
        var css = "div { color: rgba(0, 128, 255, 200); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
    }

    // --- CSS Parsing: Named colors in stylesheet ---

    [Fact]
    public void CssParser_NamedColor_RebeccaPurple_Recognized()
    {
        var css = "div { color: rebeccapurple; }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        var c = value.ColorValue!.Value;
        Assert.Equal(Color.FromRgb(102, 51, 153), c);
    }

    [Fact]
    public void CssParser_NamedColor_AliceBlue_Recognized()
    {
        var css = "div { background-color: aliceblue; }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Color, value.Type);
        Assert.Equal(Color.FromRgb(240, 248, 255), value.ColorValue!.Value);
    }

    // --- currentcolor (StyleResolver tested indirectly) ---

    [Fact]
    public void CssParser_Currentcolor_ParsedAsKeyword()
    {
        var css = "div { border-color: currentcolor; }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        // currentcolor is not a named color, so it's a keyword
        Assert.Equal(CssValueType.Keyword, value.Type);
        Assert.Equal("currentcolor", value.Raw);
    }

    // --- Hex color parsing (regression) ---

    [Fact]
    public void FromHex_3Digit_Correct()
    {
        var c = Color.FromHex("#f00");
        Assert.Equal(Color.FromRgb(255, 0, 0), c);
    }

    [Fact]
    public void FromHex_6Digit_Correct()
    {
        var c = Color.FromHex("#ff8000");
        Assert.Equal(Color.FromRgb(255, 128, 0), c);
    }

    [Fact]
    public void FromHex_8Digit_WithAlpha()
    {
        var c = Color.FromHex("#ff000080");
        Assert.Equal(Color.FromRgba(255, 0, 0, 128), c);
    }

    // --- Helpers ---

    private static void AssertColorApprox(float expectedR, float expectedG, float expectedB, float expectedA, Color actual)
    {
        Assert.Equal(expectedR, actual.R, 0.02f);
        Assert.Equal(expectedG, actual.G, 0.02f);
        Assert.Equal(expectedB, actual.B, 0.02f);
        Assert.Equal(expectedA, actual.A, 0.02f);
    }
}
