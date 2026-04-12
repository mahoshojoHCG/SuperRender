using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class FilterTests
{
    [Fact]
    public void ParseFilterFunctions_None_ReturnsNull()
    {
        Assert.Null(StyleResolver.ParseFilterFunctions("none"));
    }

    [Fact]
    public void ParseFilterFunctions_Blur()
    {
        var result = StyleResolver.ParseFilterFunctions("blur(5px)");
        Assert.NotNull(result);
        var blur = Assert.IsType<BlurFilter>(result[0]);
        Assert.Equal(5f, blur.Radius);
    }

    [Fact]
    public void ParseFilterFunctions_Brightness()
    {
        var result = StyleResolver.ParseFilterFunctions("brightness(150%)");
        Assert.NotNull(result);
        var f = Assert.IsType<BrightnessFilter>(result[0]);
        Assert.Equal(1.5f, f.Amount, 0.01f);
    }

    [Fact]
    public void ParseFilterFunctions_Contrast()
    {
        var result = StyleResolver.ParseFilterFunctions("contrast(0.5)");
        Assert.NotNull(result);
        Assert.IsType<ContrastFilter>(result[0]);
    }

    [Fact]
    public void ParseFilterFunctions_Grayscale()
    {
        var result = StyleResolver.ParseFilterFunctions("grayscale(100%)");
        Assert.NotNull(result);
        var f = Assert.IsType<GrayscaleFilter>(result[0]);
        Assert.Equal(1f, f.Amount, 0.01f);
    }

    [Fact]
    public void ParseFilterFunctions_HueRotate()
    {
        var result = StyleResolver.ParseFilterFunctions("hue-rotate(90deg)");
        Assert.NotNull(result);
        Assert.IsType<HueRotateFilter>(result[0]);
    }

    [Fact]
    public void ParseFilterFunctions_Invert()
    {
        var result = StyleResolver.ParseFilterFunctions("invert(100%)");
        Assert.NotNull(result);
        Assert.IsType<InvertFilter>(result[0]);
    }

    [Fact]
    public void ParseFilterFunctions_Sepia()
    {
        var result = StyleResolver.ParseFilterFunctions("sepia(0.8)");
        Assert.NotNull(result);
        Assert.IsType<SepiaFilter>(result[0]);
    }

    [Fact]
    public void ParseFilterFunctions_DropShadow()
    {
        var result = StyleResolver.ParseFilterFunctions("drop-shadow(4px 4px 10px black)");
        Assert.NotNull(result);
        var f = Assert.IsType<DropShadowFilter>(result[0]);
        Assert.Equal(4f, f.OffsetX);
    }

    [Fact]
    public void ParseFilterFunctions_Multiple()
    {
        var result = StyleResolver.ParseFilterFunctions("blur(2px) brightness(1.2)");
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void StyleResolver_Filter_Applied()
    {
        var style = StyleTestHelper.ResolveFirst("div { filter: blur(5px); }", "<div>test</div>");
        Assert.NotNull(style.Filter);
        Assert.Single(style.Filter);
    }

    [Fact]
    public void StyleResolver_BackdropFilter_Applied()
    {
        var style = StyleTestHelper.ResolveFirst("div { backdrop-filter: blur(10px); }", "<div>test</div>");
        Assert.NotNull(style.BackdropFilter);
    }

    [Fact]
    public void StyleResolver_MixBlendMode()
    {
        var style = StyleTestHelper.ResolveFirst("div { mix-blend-mode: multiply; }", "<div>test</div>");
        Assert.Equal("multiply", style.MixBlendMode);
    }

    [Fact]
    public void StyleResolver_Isolation_Isolate()
    {
        var style = StyleTestHelper.ResolveFirst("div { isolation: isolate; }", "<div>test</div>");
        Assert.Equal("isolate", style.Isolation);
    }

    [Fact]
    public void StyleResolver_ClipPath()
    {
        var style = StyleTestHelper.ResolveFirst("div { clip-path: circle(50%); }", "<div>test</div>");
        Assert.Equal("circle(50%)", style.ClipPath);
    }

    [Fact]
    public void StyleResolver_ClipPath_None()
    {
        var style = StyleTestHelper.ResolveFirst("div { clip-path: none; }", "<div>test</div>");
        Assert.Null(style.ClipPath);
    }
}
