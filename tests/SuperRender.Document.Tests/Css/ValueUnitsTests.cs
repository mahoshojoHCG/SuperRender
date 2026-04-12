using SuperRender.Document.Css;
using Xunit;

namespace SuperRender.Document.Tests.Css;

public class ValueUnitsTests
{
    private static CssValue Parse(string cssValue) => CssParser.ParseValueText(cssValue);

    // ===== Absolute Length Units =====

    [Theory]
    [InlineData("1cm", "cm")]
    [InlineData("10mm", "mm")]
    [InlineData("1in", "in")]
    [InlineData("6pc", "pc")]
    [InlineData("4Q", "Q")]
    public void AbsoluteUnit_ParsedAsLength(string value, string expectedUnit)
    {
        var result = Parse(value);
        Assert.Equal(CssValueType.Length, result.Type);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Fact]
    public void Cm_ToPixels_96DivBy254()
    {
        // 1cm = 96/2.54 ≈ 37.8px
        var node = new CalcValueNode(Parse("1cm"));
        var result = node.Evaluate(CalcContext.Default);
        Assert.True(Math.Abs(result - 96.0 / 2.54) < 0.1);
    }

    [Fact]
    public void Mm_ToPixels()
    {
        // 10mm = 1cm = 96/2.54px
        var node = new CalcValueNode(Parse("10mm"));
        var result = node.Evaluate(CalcContext.Default);
        Assert.True(Math.Abs(result - 96.0 / 2.54) < 0.1);
    }

    [Fact]
    public void In_ToPixels()
    {
        // 1in = 96px
        var node = new CalcValueNode(Parse("1in"));
        var result = node.Evaluate(CalcContext.Default);
        Assert.Equal(96.0, result, 0.1);
    }

    [Fact]
    public void Pc_ToPixels()
    {
        // 1pc = 16px
        var node = new CalcValueNode(Parse("1pc"));
        var result = node.Evaluate(CalcContext.Default);
        Assert.Equal(16.0, result, 0.1);
    }

    [Fact]
    public void Q_ToPixels()
    {
        // 4Q = 1mm
        var node = new CalcValueNode(Parse("4Q"));
        var result = node.Evaluate(CalcContext.Default);
        var oneMm = 96.0 / 25.4;
        Assert.True(Math.Abs(result - oneMm) < 0.1);
    }

    // ===== Font-Relative Units =====

    [Fact]
    public void Ex_ParsedAsLength()
    {
        var result = Parse("2ex");
        Assert.Equal(CssValueType.Length, result.Type);
        Assert.Equal("ex", result.Unit);
    }

    [Fact]
    public void Ch_ParsedAsLength()
    {
        var result = Parse("10ch");
        Assert.Equal(CssValueType.Length, result.Type);
        Assert.Equal("ch", result.Unit);
    }

    [Fact]
    public void Ex_Evaluation_HalfFontSize()
    {
        var node = new CalcValueNode(Parse("1ex"));
        var ctx = new CalcContext { FontSize = 16 };
        // ex ≈ 0.5 * fontSize
        Assert.Equal(8.0, node.Evaluate(ctx), 0.1);
    }

    [Fact]
    public void Lh_ParsedAndEvaluated()
    {
        var node = new CalcValueNode(Parse("2lh"));
        var ctx = new CalcContext { FontSize = 16, LineHeight = 1.5 };
        Assert.Equal(48.0, node.Evaluate(ctx), 0.1);
    }

    [Fact]
    public void Rlh_ParsedAndEvaluated()
    {
        var node = new CalcValueNode(Parse("1rlh"));
        var ctx = new CalcContext { FontSize = 16, RootLineHeight = 1.2 };
        // 1rlh = rootFontSize * rootLineHeight = 16 * 1.2 = 19.2
        Assert.Equal(19.2, node.Evaluate(ctx), 0.1);
    }

    // ===== Angle Units =====

    [Theory]
    [InlineData("90deg", "deg")]
    [InlineData("100grad", "grad")]
    [InlineData("1rad", "rad")]
    [InlineData("0.25turn", "turn")]
    public void AngleUnit_ParsedAsAngle(string value, string expectedUnit)
    {
        var result = Parse(value);
        Assert.Equal(CssValueType.Angle, result.Type);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Fact]
    public void Angle_Deg_EvaluatesToDegrees()
    {
        var node = new CalcValueNode(Parse("90deg"));
        Assert.Equal(90.0, node.Evaluate(CalcContext.Default), 0.1);
    }

    [Fact]
    public void Angle_Grad_EvaluatesToDegrees()
    {
        var node = new CalcValueNode(Parse("100grad"));
        // 100grad = 90deg
        Assert.Equal(90.0, node.Evaluate(CalcContext.Default), 0.1);
    }

    [Fact]
    public void Angle_Turn_EvaluatesToDegrees()
    {
        var node = new CalcValueNode(Parse("0.5turn"));
        // 0.5turn = 180deg
        Assert.Equal(180.0, node.Evaluate(CalcContext.Default), 0.1);
    }

    [Fact]
    public void Angle_Rad_EvaluatesToDegrees()
    {
        var node = new CalcValueNode(Parse("3.14159rad"));
        // π rad ≈ 180deg
        Assert.True(Math.Abs(node.Evaluate(CalcContext.Default) - 180.0) < 0.1);
    }

    // ===== Time Units =====

    [Theory]
    [InlineData("1s", "s")]
    [InlineData("500ms", "ms")]
    public void TimeUnit_ParsedAsTime(string value, string expectedUnit)
    {
        var result = Parse(value);
        Assert.Equal(CssValueType.Time, result.Type);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Fact]
    public void Time_Seconds_EvaluatesToMs()
    {
        var node = new CalcValueNode(Parse("2s"));
        Assert.Equal(2000.0, node.Evaluate(CalcContext.Default), 0.1);
    }

    [Fact]
    public void Time_Ms_EvaluatesDirectly()
    {
        var node = new CalcValueNode(Parse("500ms"));
        Assert.Equal(500.0, node.Evaluate(CalcContext.Default), 0.1);
    }

    // ===== Resolution Units =====

    [Theory]
    [InlineData("96dpi", "dpi")]
    [InlineData("2dppx", "dppx")]
    public void ResolutionUnit_Parsed(string value, string expectedUnit)
    {
        var result = Parse(value);
        Assert.Equal(CssValueType.Resolution, result.Type);
        Assert.Equal(expectedUnit, result.Unit);
    }

    // ===== Dynamic/Small/Large Viewport Units =====

    [Theory]
    [InlineData("50dvw", "dvw")]
    [InlineData("100dvh", "dvh")]
    [InlineData("50svw", "svw")]
    [InlineData("100svh", "svh")]
    [InlineData("50lvw", "lvw")]
    [InlineData("100lvh", "lvh")]
    public void ViewportVariants_ParsedAsLength(string value, string expectedUnit)
    {
        var result = Parse(value);
        Assert.Equal(CssValueType.Length, result.Type);
        Assert.Equal(expectedUnit, result.Unit);
    }

    [Fact]
    public void Dvw_Evaluation()
    {
        var node = new CalcValueNode(Parse("50dvw"));
        var ctx = new CalcContext { ViewportWidth = 800 };
        Assert.Equal(400.0, node.Evaluate(ctx), 0.1);
    }

    [Fact]
    public void Svh_Evaluation()
    {
        var node = new CalcValueNode(Parse("100svh"));
        var ctx = new CalcContext { SmallViewportHeight = 600 };
        Assert.Equal(600.0, node.Evaluate(ctx), 0.1);
    }

    [Fact]
    public void Lvw_Evaluation()
    {
        var node = new CalcValueNode(Parse("25lvw"));
        var ctx = new CalcContext { LargeViewportWidth = 1200 };
        Assert.Equal(300.0, node.Evaluate(ctx), 0.1);
    }
}
