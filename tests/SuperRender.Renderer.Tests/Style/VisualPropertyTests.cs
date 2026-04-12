using Xunit;

namespace SuperRender.Renderer.Tests.Style;

public class VisualPropertyTests
{
    [Fact]
    public void Float_Left()
    {
        var style = StyleTestHelper.ResolveFirst("div { float: left; }", "<div>test</div>");
        Assert.Equal("left", style.Float);
    }

    [Fact]
    public void Float_Right()
    {
        var style = StyleTestHelper.ResolveFirst("div { float: right; }", "<div>test</div>");
        Assert.Equal("right", style.Float);
    }

    [Fact]
    public void Clear_Both()
    {
        var style = StyleTestHelper.ResolveFirst("div { clear: both; }", "<div>test</div>");
        Assert.Equal("both", style.Clear);
    }

    [Fact]
    public void PointerEvents_None()
    {
        var style = StyleTestHelper.ResolveFirst("div { pointer-events: none; }", "<div>test</div>");
        Assert.Equal("none", style.PointerEvents);
    }

    [Fact]
    public void UserSelect_None()
    {
        var style = StyleTestHelper.ResolveFirst("div { user-select: none; }", "<div>test</div>");
        Assert.Equal("none", style.UserSelect);
    }

    [Fact]
    public void ObjectFit_Cover()
    {
        var style = StyleTestHelper.ResolveFirst("img { object-fit: cover; }", "<img>test</img>");
        Assert.Equal("cover", style.ObjectFit);
    }

    [Fact]
    public void ObjectFit_Contain()
    {
        var style = StyleTestHelper.ResolveFirst("img { object-fit: contain; }", "<img>test</img>");
        Assert.Equal("contain", style.ObjectFit);
    }

    [Fact]
    public void Appearance_None()
    {
        var style = StyleTestHelper.ResolveFirst("div { appearance: none; }", "<div>test</div>");
        Assert.Equal("none", style.Appearance);
    }

    [Fact]
    public void TableLayout_Fixed()
    {
        var style = StyleTestHelper.ResolveFirst("div { table-layout: fixed; }", "<div>test</div>");
        Assert.Equal("fixed", style.TableLayout);
    }

    [Fact]
    public void BorderCollapse_Collapse()
    {
        var style = StyleTestHelper.ResolveFirst("div { border-collapse: collapse; }", "<div>test</div>");
        Assert.Equal("collapse", style.BorderCollapse);
    }

    [Fact]
    public void WritingMode_VerticalRl()
    {
        var style = StyleTestHelper.ResolveFirst("div { writing-mode: vertical-rl; }", "<div>test</div>");
        Assert.Equal("vertical-rl", style.WritingMode);
    }

    [Fact]
    public void ContainerType_Size()
    {
        var style = StyleTestHelper.ResolveFirst("div { container-type: size; }", "<div>test</div>");
        Assert.Equal("size", style.ContainerType);
    }

    [Fact]
    public void ScrollSnapType_Parsed()
    {
        var style = StyleTestHelper.ResolveFirst("div { scroll-snap-type: y mandatory; }", "<div>test</div>");
        Assert.Equal("y mandatory", style.ScrollSnapType);
    }

    [Fact]
    public void Outline_Shorthand()
    {
        var style = StyleTestHelper.ResolveFirst("div { outline: 2px solid red; }", "<div>test</div>");
        Assert.Equal("solid", style.OutlineStyle);
        Assert.Equal(2f, style.OutlineWidth);
    }

    [Fact]
    public void GridAutoFlow_Row()
    {
        var style = StyleTestHelper.ResolveFirst("div { display: grid; grid-auto-flow: row; }", "<div>test</div>");
        Assert.Equal("row", style.GridAutoFlow);
    }

    [Fact]
    public void GridAutoFlow_Column()
    {
        var style = StyleTestHelper.ResolveFirst("div { display: grid; grid-auto-flow: column; }", "<div>test</div>");
        Assert.Equal("column", style.GridAutoFlow);
    }

    [Fact]
    public void GridRow_Shorthand()
    {
        var style = StyleTestHelper.ResolveFirst("div { grid-row: 1 / 3; }", "<div>test</div>");
        Assert.Equal("1", style.GridRowStart);
        Assert.Equal("3", style.GridRowEnd);
    }

    [Fact]
    public void GridColumn_Shorthand()
    {
        var style = StyleTestHelper.ResolveFirst("div { grid-column: 2 / 4; }", "<div>test</div>");
        Assert.Equal("2", style.GridColumnStart);
        Assert.Equal("4", style.GridColumnEnd);
    }

    [Fact]
    public void GridArea_Shorthand()
    {
        var style = StyleTestHelper.ResolveFirst("div { grid-area: 1 / 2 / 3 / 4; }", "<div>test</div>");
        Assert.Equal("1", style.GridRowStart);
        Assert.Equal("2", style.GridColumnStart);
        Assert.Equal("3", style.GridRowEnd);
        Assert.Equal("4", style.GridColumnEnd);
    }
}
