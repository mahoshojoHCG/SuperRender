using SuperRender.Renderer.Rendering.Layout;
using Xunit;

namespace SuperRender.Renderer.Tests.Layout;

public class BoxModelTests
{
    [Fact]
    public void ContentRect_Basic()
    {
        var box = new BoxDimensions { X = 10, Y = 20, Width = 100, Height = 50 };
        var content = box.ContentRect;
        Assert.Equal(10, content.X);
        Assert.Equal(20, content.Y);
        Assert.Equal(100, content.Width);
        Assert.Equal(50, content.Height);
    }

    [Fact]
    public void PaddingRect_ExpandsContent()
    {
        var box = new BoxDimensions
        {
            X = 10, Y = 20, Width = 100, Height = 50,
            Padding = new EdgeSizes(5, 10, 5, 10)
        };
        var padding = box.PaddingRect;
        Assert.Equal(0, padding.X);   // 10 - 10
        Assert.Equal(15, padding.Y);  // 20 - 5
        Assert.Equal(120, padding.Width);  // 100 + 10 + 10
        Assert.Equal(60, padding.Height);  // 50 + 5 + 5
    }

    [Fact]
    public void BorderRect_ExpandsPadding()
    {
        var box = new BoxDimensions
        {
            X = 20, Y = 20, Width = 100, Height = 50,
            Padding = new EdgeSizes(5, 5, 5, 5),
            Border = new EdgeSizes(2, 2, 2, 2)
        };
        var border = box.BorderRect;
        Assert.Equal(13, border.X);   // 20 - 5 - 2
        Assert.Equal(13, border.Y);   // 20 - 5 - 2
        Assert.Equal(114, border.Width);  // 100 + 5+5 + 2+2
        Assert.Equal(64, border.Height);  // 50 + 5+5 + 2+2
    }

    [Fact]
    public void MarginRect_ExpandsBorder()
    {
        var box = new BoxDimensions
        {
            X = 30, Y = 30, Width = 100, Height = 50,
            Padding = new EdgeSizes(5, 5, 5, 5),
            Border = new EdgeSizes(2, 2, 2, 2),
            Margin = new EdgeSizes(10, 10, 10, 10)
        };
        var margin = box.MarginRect;
        Assert.Equal(13, margin.X);    // 30 - 5 - 2 - 10
        Assert.Equal(13, margin.Y);    // 30 - 5 - 2 - 10
        Assert.Equal(134, margin.Width);  // 100 + (5+5) + (2+2) + (10+10)
        Assert.Equal(84, margin.Height);
    }

    [Fact]
    public void EdgeSizes_TotalCalculation()
    {
        var edges = new EdgeSizes(10, 20, 30, 40);
        Assert.Equal(60, edges.HorizontalTotal);  // 40 + 20
        Assert.Equal(40, edges.VerticalTotal);     // 10 + 30
    }
}
