using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering;
using SuperRender.Renderer.Rendering.Layout;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class CalcTests
{
    [Fact]
    public void Calc_SimplePx_Evaluates()
    {
        var css = "div { width: calc(100px + 50px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);
        Assert.NotNull(value.CalcExpr);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(150, result, 0.01);
    }

    [Fact]
    public void Calc_SubtractPx_Evaluates()
    {
        var css = "div { width: calc(200px - 30px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(170, result, 0.01);
    }

    [Fact]
    public void Calc_MultiplyPx_Evaluates()
    {
        var css = "div { width: calc(10px * 5); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(50, result, 0.01);
    }

    [Fact]
    public void Calc_DividePx_Evaluates()
    {
        var css = "div { width: calc(100px / 4); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(25, result, 0.01);
    }

    [Fact]
    public void Calc_MixedUnits_PercentAndPx()
    {
        var css = "div { width: calc(50% - 20px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var context = new CalcContext
        {
            FontSize = 16,
            ContainingBlockSize = 400,
            ViewportWidth = 800,
            ViewportHeight = 600
        };
        var result = value.CalcExpr!.Evaluate(context);
        Assert.Equal(180, result, 0.01); // 50% of 400 = 200, minus 20 = 180
    }

    [Fact]
    public void Calc_Em_Evaluates()
    {
        var css = "div { width: calc(2em + 10px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var context = new CalcContext { FontSize = 16, ContainingBlockSize = 0, ViewportWidth = 800, ViewportHeight = 600 };
        var result = value.CalcExpr!.Evaluate(context);
        Assert.Equal(42, result, 0.01); // 2*16 + 10 = 42
    }

    [Fact]
    public void Min_TwoValues_ReturnsSmaller()
    {
        var css = "div { width: min(100px, 200px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(100, result, 0.01);
    }

    [Fact]
    public void Max_TwoValues_ReturnsLarger()
    {
        var css = "div { width: max(100px, 200px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(200, result, 0.01);
    }

    [Fact]
    public void Clamp_BetweenMinMax_ReturnsPreferred()
    {
        var css = "div { width: clamp(100px, 150px, 200px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;
        Assert.Equal(CssValueType.Calc, value.Type);

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(150, result, 0.01);
    }

    [Fact]
    public void Clamp_BelowMin_ReturnsMin()
    {
        var css = "div { width: clamp(100px, 50px, 200px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(100, result, 0.01);
    }

    [Fact]
    public void Calc_OperatorPrecedence_MulBeforeAdd()
    {
        var css = "div { width: calc(10px + 5px * 3); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;

        var result = value.CalcExpr!.Evaluate(CalcContext.Default);
        Assert.Equal(25, result, 0.01); // 10 + 15 = 25, not (10+5)*3 = 45
    }

    [Fact]
    public void Calc_Vw_Evaluates()
    {
        var css = "div { width: calc(50vw + 10px); }";
        var stylesheet = new CssParser(css).Parse();
        var value = stylesheet.Rules[0].Declarations[0].Value;

        var context = new CalcContext { FontSize = 16, ContainingBlockSize = 0, ViewportWidth = 1000, ViewportHeight = 600 };
        var result = value.CalcExpr!.Evaluate(context);
        Assert.Equal(510, result, 0.01); // 50% of 1000 + 10 = 510
    }
}

public class ViewportUnitTests
{
    private static LayoutBox LayoutHtml(string html, float viewportWidth = 800, float viewportHeight = 600)
    {
        var pipeline = new RenderPipeline(new MonospaceTextMeasurer(), useUserAgentStylesheet: true);
        pipeline.LoadHtml(html);
        pipeline.Render(viewportWidth, viewportHeight);
        return pipeline.LayoutRoot!;
    }

    private static LayoutBox? FindDiv(LayoutBox box)
    {
        if (box.DomNode is SuperRender.Document.Dom.Element el && el.TagName == "div")
            return box;
        foreach (var child in box.Children)
        {
            var found = FindDiv(child);
            if (found != null) return found;
        }
        return null;
    }

    [Fact]
    public void Vw_Width_ResolvesToViewportPercentage()
    {
        var root = LayoutHtml("<html><head><style>div { width: 50vw; }</style></head><body><div>test</div></body></html>", 1000);
        var div = FindDiv(root);
        Assert.NotNull(div);
        Assert.Equal(500, div!.Dimensions.Width, 1f);
    }

    [Fact]
    public void Vh_Height_ResolvesToViewportPercentage()
    {
        var root = LayoutHtml("<html><head><style>div { height: 25vh; }</style></head><body><div>test</div></body></html>", 800, 1000);
        var div = FindDiv(root);
        Assert.NotNull(div);
        Assert.Equal(250, div!.Dimensions.Height, 1f);
    }
}
