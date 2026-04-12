using SuperRender.Renderer.Rendering.Style;
using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class TextPropertyTests
{
    [Fact]
    public void TextDecorationStyle_Default_Solid()
    {
        Assert.Equal("solid", new ComputedStyle().TextDecorationStyle);
    }

    [Fact]
    public void TextDecorationStyle_Dashed()
    {
        var style = StyleTestHelper.ResolveFirst("div { text-decoration-style: dashed; }", "<div>test</div>");
        Assert.Equal("dashed", style.TextDecorationStyle);
    }

    [Fact]
    public void TextDecorationStyle_Wavy()
    {
        var style = StyleTestHelper.ResolveFirst("div { text-decoration-style: wavy; }", "<div>test</div>");
        Assert.Equal("wavy", style.TextDecorationStyle);
    }

    [Fact]
    public void TextDecorationThickness_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { text-decoration-thickness: 3px; }", "<div>test</div>");
        Assert.Equal(3f, style.TextDecorationThickness);
    }

    [Fact]
    public void TextDecorationThickness_Auto_IsNaN()
    {
        Assert.True(float.IsNaN(new ComputedStyle().TextDecorationThickness));
    }

    [Fact]
    public void TextUnderlineOffset_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { text-underline-offset: 2px; }", "<div>test</div>");
        Assert.Equal(2f, style.TextUnderlineOffset);
    }

    [Fact]
    public void TextShadow_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { text-shadow: 2px 2px 4px black; }", "<div>test</div>");
        Assert.NotNull(style.TextShadow);
    }

    [Fact]
    public void TextShadow_None()
    {
        var style = StyleTestHelper.ResolveFirst("div { text-shadow: none; }", "<div>test</div>");
        Assert.Null(style.TextShadow);
    }

    [Fact]
    public void ListStylePosition_Inside()
    {
        var style = StyleTestHelper.ResolveFirst("li { list-style-position: inside; }", "<li>test</li>");
        Assert.Equal("inside", style.ListStylePosition);
    }

    [Fact]
    public void ListStylePosition_Default_Outside()
    {
        Assert.Equal("outside", new ComputedStyle().ListStylePosition);
    }

    [Fact]
    public void CounterReset_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { counter-reset: section 0; }", "<div>test</div>");
        Assert.Equal("section 0", style.CounterReset);
    }

    [Fact]
    public void CounterIncrement_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { counter-increment: section; }", "<div>test</div>");
        Assert.Equal("section", style.CounterIncrement);
    }

    [Fact]
    public void FontStretch_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { font-stretch: condensed; }", "<div>test</div>");
        Assert.Equal("condensed", style.FontStretch);
    }

    [Fact]
    public void VerticalAlign_Middle()
    {
        var style = StyleTestHelper.ResolveFirst("span { vertical-align: middle; }", "<span>test</span>");
        Assert.Equal("middle", style.VerticalAlign);
    }

    [Fact]
    public void VerticalAlign_Default_Baseline()
    {
        Assert.Equal("baseline", new ComputedStyle().VerticalAlign);
    }

    [Fact]
    public void FontShorthand_SizeAndFamily()
    {
        var style = StyleTestHelper.ResolveFirst("div { font: 20px Arial; }", "<div>test</div>");
        Assert.Equal(20f, style.FontSize);
    }

    [Fact]
    public void FontShorthand_BoldSizeFamily()
    {
        var style = StyleTestHelper.ResolveFirst("div { font: bold 16px sans-serif; }", "<div>test</div>");
        Assert.Equal(700, style.FontWeight);
        Assert.Equal(16f, style.FontSize);
    }

    [Fact]
    public void FontShorthand_ItalicBoldSizeFamily()
    {
        var style = StyleTestHelper.ResolveFirst("div { font: italic bold 14px serif; }", "<div>test</div>");
        Assert.Equal(FontStyleType.Italic, style.FontStyle);
        Assert.Equal(700, style.FontWeight);
    }
}
