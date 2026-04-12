using DomDocument = SuperRender.Document.Dom.Document;
using SuperRender.Document.Css;
using SuperRender.Document.Dom;
using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class MathFunctionsTests
{
    private static double EvalCalc(string css)
    {
        var value = CssParser.ParseValueText(css);
        Assert.Equal(CssValueType.Calc, value.Type);
        Assert.NotNull(value.CalcExpr);
        return value.CalcExpr!.Evaluate(CalcContext.Default);
    }

    // ===== abs() =====

    [Fact]
    public void Abs_PositiveValue()
    {
        Assert.Equal(10.0, EvalCalc("abs(10)"), 0.1);
    }

    [Fact]
    public void Abs_NegativeValue()
    {
        Assert.Equal(5.0, EvalCalc("abs(-5)"), 0.1);
    }

    // ===== sign() =====

    [Fact]
    public void Sign_PositiveValue()
    {
        Assert.Equal(1.0, EvalCalc("sign(42)"), 0.1);
    }

    [Fact]
    public void Sign_NegativeValue()
    {
        Assert.Equal(-1.0, EvalCalc("sign(-7)"), 0.1);
    }

    [Fact]
    public void Sign_Zero()
    {
        Assert.Equal(0.0, EvalCalc("sign(0)"), 0.1);
    }

    // ===== round() =====

    [Fact]
    public void Round_SingleArg()
    {
        Assert.Equal(3.0, EvalCalc("round(2.7)"), 0.1);
    }

    [Fact]
    public void Round_TwoArgs_ToStep()
    {
        // round(13, 5) → round(13/5)*5 = round(2.6)*5 = 3*5 = 15
        Assert.Equal(15.0, EvalCalc("round(13, 5)"), 0.1);
    }

    // ===== mod() =====

    [Fact]
    public void Mod_Basic()
    {
        Assert.Equal(1.0, EvalCalc("mod(7, 3)"), 0.1);
    }

    [Fact]
    public void Mod_NoRemainder()
    {
        Assert.Equal(0.0, EvalCalc("mod(6, 3)"), 0.1);
    }

    // ===== rem() =====

    [Fact]
    public void Rem_Basic()
    {
        Assert.Equal(1.0, EvalCalc("rem(7, 3)"), 0.1);
    }

    // ===== Trigonometric =====

    [Fact]
    public void Sin_0_Returns0()
    {
        Assert.True(Math.Abs(EvalCalc("sin(0)")) < 0.01);
    }

    [Fact]
    public void Sin_90_Returns1()
    {
        Assert.True(Math.Abs(EvalCalc("sin(90)") - 1.0) < 0.01);
    }

    [Fact]
    public void Cos_0_Returns1()
    {
        Assert.True(Math.Abs(EvalCalc("cos(0)") - 1.0) < 0.01);
    }

    [Fact]
    public void Cos_180_ReturnsMinus1()
    {
        Assert.True(Math.Abs(EvalCalc("cos(180)") - (-1.0)) < 0.01);
    }

    [Fact]
    public void Tan_45_Returns1()
    {
        Assert.True(Math.Abs(EvalCalc("tan(45)") - 1.0) < 0.01);
    }

    [Fact]
    public void Asin_1_Returns90()
    {
        Assert.True(Math.Abs(EvalCalc("asin(1)") - 90.0) < 0.1);
    }

    [Fact]
    public void Acos_1_Returns0()
    {
        Assert.True(Math.Abs(EvalCalc("acos(1)")) < 0.1);
    }

    [Fact]
    public void Atan_1_Returns45()
    {
        Assert.True(Math.Abs(EvalCalc("atan(1)") - 45.0) < 0.1);
    }

    [Fact]
    public void Atan2_1_1_Returns45()
    {
        Assert.True(Math.Abs(EvalCalc("atan2(1, 1)") - 45.0) < 0.1);
    }

    // ===== pow() / sqrt() =====

    [Fact]
    public void Pow_2_3_Returns8()
    {
        Assert.Equal(8.0, EvalCalc("pow(2, 3)"), 0.1);
    }

    [Fact]
    public void Sqrt_16_Returns4()
    {
        Assert.Equal(4.0, EvalCalc("sqrt(16)"), 0.1);
    }

    // ===== hypot() =====

    [Fact]
    public void Hypot_3_4_Returns5()
    {
        Assert.Equal(5.0, EvalCalc("hypot(3, 4)"), 0.1);
    }

    // ===== log() / exp() =====

    [Fact]
    public void Log_e_Returns1()
    {
        // log(e) = 1 where e ≈ 2.71828
        Assert.True(Math.Abs(EvalCalc("log(2.71828)") - 1.0) < 0.01);
    }

    [Fact]
    public void Log_Base10()
    {
        // log(100, 10) = 2
        Assert.Equal(2.0, EvalCalc("log(100, 10)"), 0.1);
    }

    [Fact]
    public void Exp_0_Returns1()
    {
        Assert.Equal(1.0, EvalCalc("exp(0)"), 0.1);
    }

    [Fact]
    public void Exp_1_ReturnsE()
    {
        Assert.True(Math.Abs(EvalCalc("exp(1)") - Math.E) < 0.01);
    }

    // ===== Nested math in calc() =====

    [Fact]
    public void Calc_WithAbsNested()
    {
        var value = CssParser.ParseValueText("calc(abs(-10) + 5)");
        Assert.Equal(CssValueType.Calc, value.Type);
        Assert.NotNull(value.CalcExpr);
        Assert.Equal(15.0, value.CalcExpr!.Evaluate(CalcContext.Default), 0.1);
    }

    [Fact]
    public void Calc_WithSqrtNested()
    {
        var value = CssParser.ParseValueText("calc(sqrt(144) * 2)");
        Assert.Equal(CssValueType.Calc, value.Type);
        Assert.NotNull(value.CalcExpr);
        Assert.Equal(24.0, value.CalcExpr!.Evaluate(CalcContext.Default), 0.1);
    }

    // ===== ResolveAngle =====

    [Fact]
    public void ResolveAngle_Degrees()
    {
        var value = CssParser.ParseValueText("45deg");
        var result = StyleResolver.ResolveAngle(value);
        Assert.Equal(45f, result, 0.1f);
    }

    [Fact]
    public void ResolveAngle_Grad()
    {
        var value = CssParser.ParseValueText("200grad");
        var result = StyleResolver.ResolveAngle(value);
        Assert.Equal(180f, result, 0.1f);
    }

    [Fact]
    public void ResolveAngle_Turn()
    {
        var value = CssParser.ParseValueText("0.25turn");
        var result = StyleResolver.ResolveAngle(value);
        Assert.Equal(90f, result, 0.1f);
    }

    [Fact]
    public void ResolveAngle_Rad()
    {
        var value = CssParser.ParseValueText("1.5708rad");
        var result = StyleResolver.ResolveAngle(value);
        Assert.True(Math.Abs(result - 90f) < 0.5f);
    }

    // ===== ResolveTime =====

    [Fact]
    public void ResolveTime_Seconds()
    {
        var value = CssParser.ParseValueText("2s");
        var result = StyleResolver.ResolveTime(value);
        Assert.Equal(2000f, result, 0.1f);
    }

    [Fact]
    public void ResolveTime_Milliseconds()
    {
        var value = CssParser.ParseValueText("300ms");
        var result = StyleResolver.ResolveTime(value);
        Assert.Equal(300f, result, 0.1f);
    }

    // ===== Style resolution with new units =====

    private static ComputedStyle ResolveStyle(string css, string bodyHtml)
    {
        var doc = new DomDocument();
        var html = new Element("html");
        var head = new Element("head");
        var body = new Element("body");
        doc.AppendChild(html);
        html.AppendChild(head);
        html.AppendChild(body);

        if (!string.IsNullOrEmpty(css))
        {
            var stylesheet = new CssParser(css).Parse();
            doc.Stylesheets.Add(stylesheet);
        }

        var parser = new SuperRender.Document.Html.HtmlParser(bodyHtml);
        var parsedDoc = parser.Parse();
        if (parsedDoc.Body != null)
        {
            foreach (var child in parsedDoc.Body.Children.ToList())
            {
                child.Parent?.RemoveChild(child);
                body.AppendChild(child);
            }
        }

        var target = body.Children.OfType<Element>().FirstOrDefault()!;
        var resolver = new StyleResolver(doc.Stylesheets);
        return resolver.Resolve(target);
    }

    [Fact]
    public void CmUnit_ResolvedInStyleWidth()
    {
        var style = ResolveStyle("div { width: 1cm; }", "<div>test</div>");
        // 1cm ≈ 37.8px
        Assert.True(style.Width > 37 && style.Width < 39);
    }

    [Fact]
    public void InUnit_ResolvedInStyleWidth()
    {
        var style = ResolveStyle("div { width: 1in; }", "<div>test</div>");
        Assert.Equal(96f, style.Width, 0.1f);
    }

    [Fact]
    public void PtUnit_ResolvedInFontSize()
    {
        var style = ResolveStyle("div { font-size: 12pt; }", "<div>test</div>");
        // 12pt = 12 * 96/72 = 16px
        Assert.Equal(16f, style.FontSize, 0.1f);
    }

    [Fact]
    public void ExUnit_ResolvedInPadding()
    {
        var style = ResolveStyle("div { padding-top: 2ex; }", "<div>test</div>");
        // 2ex ≈ 2 * 0.5 * 16 = 16px (default font size)
        Assert.Equal(16f, style.Padding.Top, 0.1f);
    }
}
