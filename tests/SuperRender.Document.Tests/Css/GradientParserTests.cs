using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class GradientParserTests
{
    [Fact]
    public void LinearGradient_TwoColors_DefaultAngle()
    {
        var value = CssParser.ParseValueText("linear-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(180f, lg.AngleDeg);
        Assert.Equal(2, lg.ColorStops.Count);
    }

    [Fact]
    public void LinearGradient_ToRight()
    {
        var value = CssParser.ParseValueText("linear-gradient(to right, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(90f, lg.AngleDeg);
    }

    [Fact]
    public void LinearGradient_ToTop()
    {
        var value = CssParser.ParseValueText("linear-gradient(to top, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(0f, lg.AngleDeg);
    }

    [Fact]
    public void LinearGradient_ToBottomRight()
    {
        var value = CssParser.ParseValueText("linear-gradient(to bottom right, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(135f, lg.AngleDeg);
    }

    [Fact]
    public void LinearGradient_AngleDeg()
    {
        var value = CssParser.ParseValueText("linear-gradient(45deg, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(45f, lg.AngleDeg, 0.1f);
    }

    [Fact]
    public void LinearGradient_AngleTurn()
    {
        var value = CssParser.ParseValueText("linear-gradient(0.25turn, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(90f, lg.AngleDeg, 0.1f);
    }

    [Fact]
    public void LinearGradient_ThreeColors()
    {
        var value = CssParser.ParseValueText("linear-gradient(red, green, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(3, lg.ColorStops.Count);
        Assert.Equal(0f, lg.ColorStops[0].Position!.Value, 0.01f);
        Assert.Equal(0.5f, lg.ColorStops[1].Position!.Value, 0.01f);
        Assert.Equal(1f, lg.ColorStops[2].Position!.Value, 0.01f);
    }

    [Fact]
    public void LinearGradient_ExplicitPositions()
    {
        var value = CssParser.ParseValueText("linear-gradient(red 10%, blue 90%)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(2, lg.ColorStops.Count);
        Assert.Equal(0.1f, lg.ColorStops[0].Position!.Value, 0.01f);
        Assert.Equal(0.9f, lg.ColorStops[1].Position!.Value, 0.01f);
    }

    [Fact]
    public void LinearGradient_HexColors()
    {
        var value = CssParser.ParseValueText("linear-gradient(#ff0000, #0000ff)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(2, lg.ColorStops.Count);
    }

    [Fact]
    public void LinearGradient_RgbFunctionColors()
    {
        var value = CssParser.ParseValueText("linear-gradient(rgb(255, 0, 0), rgb(0, 0, 255))");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(2, lg.ColorStops.Count);
    }

    [Fact]
    public void LinearGradient_FirstStopDefaultsTo0()
    {
        var value = CssParser.ParseValueText("linear-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(0f, lg.ColorStops[0].Position!.Value, 0.01f);
    }

    [Fact]
    public void LinearGradient_LastStopDefaultsTo1()
    {
        var value = CssParser.ParseValueText("linear-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(1f, lg.ColorStops[1].Position!.Value, 0.01f);
    }

    [Fact]
    public void LinearGradient_ToLeft()
    {
        var value = CssParser.ParseValueText("linear-gradient(to left, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(270f, lg.AngleDeg);
    }

    [Fact]
    public void LinearGradient_ToTopLeft()
    {
        var value = CssParser.ParseValueText("linear-gradient(to top left, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(315f, lg.AngleDeg);
    }

    [Fact]
    public void LinearGradient_FourStops_EvenlyDistributed()
    {
        var value = CssParser.ParseValueText("linear-gradient(red, orange, yellow, green)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(4, lg.ColorStops.Count);
        Assert.Equal(0f, lg.ColorStops[0].Position!.Value, 0.01f);
        Assert.InRange(lg.ColorStops[1].Position!.Value, 0.3f, 0.4f);
        Assert.InRange(lg.ColorStops[2].Position!.Value, 0.6f, 0.7f);
        Assert.Equal(1f, lg.ColorStops[3].Position!.Value, 0.01f);
    }

    // Radial gradient tests

    [Fact]
    public void RadialGradient_TwoColors_DefaultShape()
    {
        var value = CssParser.ParseValueText("radial-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(RadialGradientShape.Ellipse, rg.Shape);
        Assert.Equal(2, rg.ColorStops.Count);
    }

    [Fact]
    public void RadialGradient_Circle()
    {
        var value = CssParser.ParseValueText("radial-gradient(circle, red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(RadialGradientShape.Circle, rg.Shape);
    }

    [Fact]
    public void RadialGradient_CircleAtCenter()
    {
        var value = CssParser.ParseValueText("radial-gradient(circle at center, red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(RadialGradientShape.Circle, rg.Shape);
        Assert.Equal(0.5f, rg.CenterX, 0.01f);
        Assert.Equal(0.5f, rg.CenterY, 0.01f);
    }

    [Fact]
    public void RadialGradient_ClosestSide()
    {
        var value = CssParser.ParseValueText("radial-gradient(closest-side, red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(RadialGradientSize.ClosestSide, rg.Size);
    }

    [Fact]
    public void RadialGradient_AtTopLeft()
    {
        var value = CssParser.ParseValueText("radial-gradient(circle at left top, red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(0f, rg.CenterX, 0.01f);
        Assert.Equal(0f, rg.CenterY, 0.01f);
    }

    // Conic gradient tests

    [Fact]
    public void ConicGradient_TwoColors_Default()
    {
        var value = CssParser.ParseValueText("conic-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        var cg = Assert.IsType<ConicGradient>(value.Gradient);
        Assert.Equal(2, cg.ColorStops.Count);
    }

    [Fact]
    public void ConicGradient_FromAngle()
    {
        var value = CssParser.ParseValueText("conic-gradient(from 45deg, red, blue)");
        Assert.NotNull(value.Gradient);
        var cg = Assert.IsType<ConicGradient>(value.Gradient);
        Assert.Equal(45f, cg.FromAngleDeg, 0.1f);
    }

    [Fact]
    public void ConicGradient_AtCenter()
    {
        var value = CssParser.ParseValueText("conic-gradient(from 0deg at center, red, blue)");
        Assert.NotNull(value.Gradient);
        var cg = Assert.IsType<ConicGradient>(value.Gradient);
        Assert.Equal(0.5f, cg.CenterX, 0.01f);
        Assert.Equal(0.5f, cg.CenterY, 0.01f);
    }

    // CSS parsed in stylesheet context

    [Fact]
    public void Stylesheet_BackgroundImage_LinearGradient()
    {
        var css = "div { background-image: linear-gradient(to right, red, blue); }";
        var stylesheet = new CssParser(css).Parse();
        var decl = stylesheet.Rules[0].Declarations[0];
        Assert.Equal("background-image", decl.Property);
        Assert.NotNull(decl.Value.Gradient);
    }

    [Fact]
    public void LinearGradient_ToBottom_IsDefault()
    {
        var value = CssParser.ParseValueText("linear-gradient(to bottom, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(180f, lg.AngleDeg);
    }

    [Fact]
    public void LinearGradient_180deg_SameAsToBottom()
    {
        var value = CssParser.ParseValueText("linear-gradient(180deg, red, blue)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        Assert.Equal(180f, lg.AngleDeg, 0.1f);
    }

    [Fact]
    public void RadialGradient_ThreeStops()
    {
        var value = CssParser.ParseValueText("radial-gradient(red, yellow, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(3, rg.ColorStops.Count);
    }

    [Fact]
    public void ConicGradient_MultipleStops()
    {
        var value = CssParser.ParseValueText("conic-gradient(red, yellow, green, blue)");
        Assert.NotNull(value.Gradient);
        var cg = Assert.IsType<ConicGradient>(value.Gradient);
        Assert.Equal(4, cg.ColorStops.Count);
    }

    [Fact]
    public void SingleColor_ReturnsNull()
    {
        var value = CssParser.ParseValueText("linear-gradient(red)");
        // Single color is insufficient for a gradient
        Assert.Null(value.Gradient);
    }

    [Fact]
    public void RepeatingLinearGradient_Parsed()
    {
        var value = CssParser.ParseValueText("repeating-linear-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        Assert.IsType<LinearGradient>(value.Gradient);
    }

    [Fact]
    public void RepeatingRadialGradient_Parsed()
    {
        var value = CssParser.ParseValueText("repeating-radial-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        Assert.IsType<RadialGradient>(value.Gradient);
    }

    [Fact]
    public void LinearGradient_NamedColors()
    {
        var value = CssParser.ParseValueText("linear-gradient(white, black)");
        Assert.NotNull(value.Gradient);
        var lg = Assert.IsType<LinearGradient>(value.Gradient);
        var firstStop = lg.ColorStops[0];
        Assert.Equal(1f, firstStop.Color.R, 0.01f);
        Assert.Equal(1f, firstStop.Color.G, 0.01f);
        Assert.Equal(1f, firstStop.Color.B, 0.01f);
    }

    [Fact]
    public void RadialGradient_FarthestCorner_Default()
    {
        var value = CssParser.ParseValueText("radial-gradient(red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(RadialGradientSize.FarthestCorner, rg.Size);
    }

    [Fact]
    public void RadialGradient_FarthestSide()
    {
        var value = CssParser.ParseValueText("radial-gradient(farthest-side, red, blue)");
        Assert.NotNull(value.Gradient);
        var rg = Assert.IsType<RadialGradient>(value.Gradient);
        Assert.Equal(RadialGradientSize.FarthestSide, rg.Size);
    }
}
