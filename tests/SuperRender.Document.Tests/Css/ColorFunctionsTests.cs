using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class ColorFunctionsTests
{
    private static Color ParseColor(string cssValue)
    {
        var result = CssParser.ParseValueText(cssValue);
        Assert.Equal(CssValueType.Color, result.Type);
        Assert.NotNull(result.ColorValue);
        return result.ColorValue!.Value;
    }

    private static void AssertColorClose(Color expected, Color actual, float tolerance = 0.02f)
    {
        Assert.True(Math.Abs(expected.R - actual.R) <= tolerance,
            $"R: expected {expected.R:F3}, got {actual.R:F3}");
        Assert.True(Math.Abs(expected.G - actual.G) <= tolerance,
            $"G: expected {expected.G:F3}, got {actual.G:F3}");
        Assert.True(Math.Abs(expected.B - actual.B) <= tolerance,
            $"B: expected {expected.B:F3}, got {actual.B:F3}");
        Assert.True(Math.Abs(expected.A - actual.A) <= tolerance,
            $"A: expected {expected.A:F3}, got {actual.A:F3}");
    }

    // ===== HWB =====

    [Fact]
    public void Hwb_PureRed_Parses()
    {
        var color = ParseColor("hwb(0 0% 0%)");
        AssertColorClose(Color.FromRgb(255, 0, 0), color);
    }

    [Fact]
    public void Hwb_WithWhiteness_ProducesLighterColor()
    {
        var color = ParseColor("hwb(0 50% 0%)");
        // 50% whiteness on red: rgb channels shifted toward white
        Assert.True(color.R > 0.5f);
        Assert.True(color.G > 0.4f);
        Assert.Equal(1f, color.A);
    }

    [Fact]
    public void Hwb_WithBlackness_ProducesDarkerColor()
    {
        var color = ParseColor("hwb(0 0% 50%)");
        // 50% blackness on red
        Assert.True(color.R > 0.3f && color.R < 0.6f);
        Assert.True(color.G < 0.1f);
    }

    [Fact]
    public void Hwb_WhitenessPlusBlacknessOver100_ProducesGray()
    {
        var color = ParseColor("hwb(0 60% 60%)");
        // w + b >= 1, result is gray = w/(w+b) = 0.5
        AssertColorClose(new Color(0.5f, 0.5f, 0.5f, 1f), color);
    }

    [Fact]
    public void Hwb_WithAlpha_Parses()
    {
        var color = ParseColor("hwb(120 0% 0% / 0.5)");
        AssertColorClose(Color.FromRgb(0, 255, 0), new Color(color.R, color.G, color.B, 1f));
        AssertColorClose(new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, color.A));
    }

    // ===== Lab =====

    [Fact]
    public void Lab_Black_Parses()
    {
        var color = ParseColor("lab(0 0 0)");
        AssertColorClose(Color.Black, color, 0.05f);
    }

    [Fact]
    public void Lab_White_Parses()
    {
        var color = ParseColor("lab(100 0 0)");
        AssertColorClose(Color.White, color, 0.05f);
    }

    [Fact]
    public void Lab_WithAlpha_Parses()
    {
        var color = ParseColor("lab(50 40 -20 / 0.5)");
        Assert.True(color.A > 0.45f && color.A < 0.55f);
    }

    // ===== LCH =====

    [Fact]
    public void Lch_Gray_Parses()
    {
        // L=50, C=0 (achromatic) → medium gray
        var color = ParseColor("lch(50 0 0)");
        Assert.True(color.R > 0.35f && color.R < 0.55f);
        Assert.True(Math.Abs(color.R - color.G) < 0.05f);
        Assert.True(Math.Abs(color.G - color.B) < 0.05f);
    }

    [Fact]
    public void Lch_WithAlpha_Parses()
    {
        var color = ParseColor("lch(50 30 270 / 0.8)");
        Assert.True(color.A > 0.75f && color.A < 0.85f);
    }

    // ===== OKLab =====

    [Fact]
    public void Oklab_Black_Parses()
    {
        var color = ParseColor("oklab(0 0 0)");
        AssertColorClose(Color.Black, color, 0.05f);
    }

    [Fact]
    public void Oklab_White_Parses()
    {
        var color = ParseColor("oklab(1 0 0)");
        AssertColorClose(Color.White, color, 0.05f);
    }

    [Fact]
    public void Oklab_WithAlpha_Parses()
    {
        var color = ParseColor("oklab(0.5 0.1 -0.1 / 0.7)");
        Assert.True(color.A > 0.65f && color.A < 0.75f);
    }

    // ===== OKLCH =====

    [Fact]
    public void Oklch_Gray_Parses()
    {
        var color = ParseColor("oklch(0.5 0 0)");
        Assert.True(color.R > 0.35f && color.R < 0.55f);
        Assert.True(Math.Abs(color.R - color.G) < 0.05f);
    }

    [Fact]
    public void Oklch_WithAlpha_Parses()
    {
        var color = ParseColor("oklch(0.7 0.1 150 / 0.9)");
        Assert.True(color.A > 0.85f && color.A < 0.95f);
    }

    // ===== color() =====

    [Fact]
    public void Color_Srgb_Red_Parses()
    {
        var color = ParseColor("color(srgb 1 0 0)");
        AssertColorClose(Color.FromRgb(255, 0, 0), color);
    }

    [Fact]
    public void Color_Srgb_WithAlpha_Parses()
    {
        var color = ParseColor("color(srgb 0 0.5 1 / 0.5)");
        Assert.True(color.A > 0.45f && color.A < 0.55f);
        Assert.True(color.R < 0.05f);
        Assert.True(color.G > 0.45f && color.G < 0.55f);
        Assert.True(color.B > 0.95f);
    }

    // ===== color-mix() =====

    [Fact]
    public void ColorMix_EqualMix_Parses()
    {
        var color = ParseColor("color-mix(in srgb, red, blue)");
        // 50% red + 50% blue = purple-ish
        Assert.True(color.R > 0.45f);
        Assert.True(color.B > 0.45f);
    }

    [Fact]
    public void ColorMix_WeightedMix_Parses()
    {
        var color = ParseColor("color-mix(in srgb, red 75%, blue)");
        // 75% red, 25% blue
        Assert.True(color.R > color.B);
    }

    // ===== light-dark() =====

    [Fact]
    public void LightDark_ReturnsLightColor()
    {
        var color = ParseColor("light-dark(red, blue)");
        // In light mode (default), returns red
        AssertColorClose(Color.FromRgb(255, 0, 0), color);
    }

    // ===== rgb() improvements =====

    [Fact]
    public void Rgb_PercentageAlpha_CommaSyntax()
    {
        var color = ParseColor("rgba(255, 0, 0, 50%)");
        AssertColorClose(new Color(1f, 0f, 0f, 0.5f), color);
    }

    [Fact]
    public void Rgb_PercentageValues()
    {
        var color = ParseColor("rgb(100%, 0%, 0%)");
        AssertColorClose(Color.FromRgb(255, 0, 0), color);
    }

    [Fact]
    public void Rgb_NoneKeyword()
    {
        var color = ParseColor("rgb(none 128 255)");
        Assert.True(color.R < 0.01f);
        Assert.True(color.G > 0.45f && color.G < 0.55f);
        Assert.True(color.B > 0.95f);
    }

    [Fact]
    public void Rgb_SpaceSeparated_PercentageAlpha()
    {
        var color = ParseColor("rgb(0 0 255 / 50%)");
        AssertColorClose(new Color(0f, 0f, 1f, 0.5f), color);
    }

    [Fact]
    public void Rgb_NoneAlpha()
    {
        var color = ParseColor("rgb(255 0 0 / none)");
        Assert.True(color.A < 0.01f);
    }

    // ===== hsl() improvements =====

    [Fact]
    public void Hsl_NoneKeyword()
    {
        var color = ParseColor("hsl(none 100% 50%)");
        // none hue = 0 = red
        AssertColorClose(Color.FromRgb(255, 0, 0), color);
    }

    [Fact]
    public void Hsl_PercentageAlpha()
    {
        var color = ParseColor("hsla(0, 100%, 50%, 50%)");
        AssertColorClose(new Color(1f, 0f, 0f, 0.5f), color);
    }

    // ===== System Colors =====

    [Fact]
    public void SystemColor_Canvas_Parses()
    {
        var result = CssParser.ParseValueText("Canvas");
        Assert.Equal(CssValueType.Color, result.Type);
        Assert.NotNull(result.ColorValue);
        AssertColorClose(Color.White, result.ColorValue!.Value);
    }

    [Fact]
    public void SystemColor_CanvasText_Parses()
    {
        var result = CssParser.ParseValueText("CanvasText");
        Assert.Equal(CssValueType.Color, result.Type);
        AssertColorClose(Color.Black, result.ColorValue!.Value);
    }

    [Fact]
    public void SystemColor_LinkText_Parses()
    {
        var result = CssParser.ParseValueText("LinkText");
        Assert.Equal(CssValueType.Color, result.Type);
        var color = result.ColorValue!.Value;
        Assert.True(color.B > 0.9f); // Blue link color
    }

    [Fact]
    public void SystemColor_Highlight_Parses()
    {
        Assert.True(Color.TryFromSystemColor("Highlight", out var color));
        Assert.True(color.B > 0.5f);
    }

    [Fact]
    public void SystemColor_GrayText_Parses()
    {
        Assert.True(Color.TryFromSystemColor("GrayText", out var color));
        AssertColorClose(Color.FromRgb(128, 128, 128), color);
    }

    [Fact]
    public void SystemColor_Unknown_ReturnsFalse()
    {
        Assert.False(Color.TryFromSystemColor("NonExistentColor", out _));
    }

    // ===== currentcolor keyword =====

    [Fact]
    public void Currentcolor_ParsedAsKeyword()
    {
        var result = CssParser.ParseValueText("currentcolor");
        Assert.Equal(CssValueType.Keyword, result.Type);
        Assert.Equal("currentcolor", result.Raw);
    }

    [Fact]
    public void Currentcolor_CaseInsensitive()
    {
        var result = CssParser.ParseValueText("currentColor");
        Assert.Equal(CssValueType.Keyword, result.Type);
        Assert.Equal("currentColor", result.Raw);
    }

    // ===== Color struct factory methods =====

    [Fact]
    public void FromHwb_PureHue_CorrectRgb()
    {
        var red = Color.FromHwb(0, 0, 0);
        AssertColorClose(Color.FromRgb(255, 0, 0), red);

        var green = Color.FromHwb(120, 0, 0);
        AssertColorClose(Color.FromRgb(0, 255, 0), green);

        var blue = Color.FromHwb(240, 0, 0);
        AssertColorClose(Color.FromRgb(0, 0, 255), blue);
    }

    [Fact]
    public void FromHwba_WithAlpha()
    {
        var color = Color.FromHwba(0, 0, 0, 0.5);
        Assert.True(Math.Abs(color.A - 0.5f) < 0.01f);
    }

    [Fact]
    public void FromLab_Roundtrip_Black()
    {
        var c = Color.FromLab(0, 0, 0);
        AssertColorClose(Color.Black, c, 0.05f);
    }

    [Fact]
    public void FromLab_Roundtrip_White()
    {
        var c = Color.FromLab(100, 0, 0);
        AssertColorClose(Color.White, c, 0.05f);
    }

    [Fact]
    public void FromOklab_Black()
    {
        var c = Color.FromOklab(0, 0, 0);
        AssertColorClose(Color.Black, c, 0.05f);
    }

    [Fact]
    public void FromOklab_White()
    {
        var c = Color.FromOklab(1, 0, 0);
        AssertColorClose(Color.White, c, 0.05f);
    }

    [Fact]
    public void FromOklch_Achromatic()
    {
        // C=0 → achromatic
        var c = Color.FromOklch(0.5, 0, 0);
        Assert.True(Math.Abs(c.R - c.G) < 0.05f);
        Assert.True(Math.Abs(c.G - c.B) < 0.05f);
    }

    [Fact]
    public void ColorMix_Default5050()
    {
        var c = Color.ColorMix(Color.FromRgb(255, 0, 0), Color.FromRgb(0, 0, 255));
        Assert.True(c.R > 0.45f && c.R < 0.55f);
        Assert.True(c.B > 0.45f && c.B < 0.55f);
    }

    [Fact]
    public void ColorMix_Weighted()
    {
        var c = Color.ColorMix(Color.White, Color.Black, 0.75, 0.25);
        Assert.True(c.R > 0.7f && c.R < 0.8f);
    }

    [Fact]
    public void LightDark_LightMode_ReturnsLight()
    {
        var result = Color.LightDark(Color.White, Color.Black, isDarkMode: false);
        Assert.Equal(Color.White, result);
    }

    [Fact]
    public void LightDark_DarkMode_ReturnsDark()
    {
        var result = Color.LightDark(Color.White, Color.Black, isDarkMode: true);
        Assert.Equal(Color.Black, result);
    }

    // ===== Color struct: existing HSL roundtrips =====

    [Fact]
    public void FromHsl_Red()
    {
        var c = Color.FromHsl(0, 1, 0.5);
        AssertColorClose(Color.FromRgb(255, 0, 0), c);
    }

    [Fact]
    public void FromHsl_Cyan()
    {
        var c = Color.FromHsl(180, 1, 0.5);
        AssertColorClose(Color.FromRgb(0, 255, 255), c);
    }
}
