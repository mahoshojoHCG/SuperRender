using SuperRender.Core;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;
using SuperRender.Core.Style;
using Xunit;

namespace SuperRender.Tests.Painting;

public class TextDecorationPaintTests
{
    private static LayoutBox CreateTextBox(string text, ComputedStyle style)
    {
        var box = new LayoutBox
        {
            Style = style,
            BoxType = LayoutBoxType.Inline,
            Dimensions = new BoxDimensions { X = 0, Y = 0, Width = 200, Height = 20 },
        };
        box.TextRuns =
        [
            new TextRun
            {
                Text = text,
                X = 10,
                Y = 20,
                Width = 100,
                Height = 19.2f,
                Style = style,
            }
        ];
        return box;
    }

    [Fact]
    public void Underline_GeneratesFillRect_BelowText()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.Inline,
            TextDecorationLine = TextDecorationLine.Underline,
            Color = Color.FromName("blue"),
            FontSize = 16f,
        };
        var box = CreateTextBox("hello", style);
        var paintList = Painter.Paint(box);

        var rects = paintList.Commands.OfType<FillRectCommand>().ToList();
        Assert.Contains(rects, r =>
            r.Rect.X == 10 &&
            r.Rect.Y == 36f && // Y(20) + FontSize(16)
            r.Rect.Width == 100 &&
            r.Color.B > 0);
    }

    [Fact]
    public void LineThrough_GeneratesFillRect_AtMidpoint()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.Inline,
            TextDecorationLine = TextDecorationLine.LineThrough,
            Color = Color.Black,
            FontSize = 16f,
        };
        var box = CreateTextBox("hello", style);
        var paintList = Painter.Paint(box);

        var rects = paintList.Commands.OfType<FillRectCommand>().ToList();
        Assert.Contains(rects, r =>
            r.Rect.X == 10 &&
            r.Rect.Y == 28f && // Y(20) + FontSize(16) * 0.5
            r.Rect.Width == 100);
    }

    [Fact]
    public void NoDecoration_NoExtraFillRects()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.Inline,
            TextDecorationLine = TextDecorationLine.None,
            Color = Color.Black,
            FontSize = 16f,
        };
        var box = CreateTextBox("hello", style);
        var paintList = Painter.Paint(box);

        // Only the DrawTextCommand, no FillRectCommand for decoration
        var rects = paintList.Commands.OfType<FillRectCommand>().ToList();
        Assert.Empty(rects);
    }

    [Fact]
    public void DrawTextCommand_CarriesFontWeight()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.Inline,
            FontWeight = 700,
            Color = Color.Black,
            FontSize = 16f,
        };
        var box = CreateTextBox("bold", style);
        var paintList = Painter.Paint(box);

        var textCmd = paintList.Commands.OfType<DrawTextCommand>().First();
        Assert.Equal(700, textCmd.FontWeight);
    }

    [Fact]
    public void DrawTextCommand_CarriesFontStyle()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.Inline,
            FontStyle = FontStyleType.Italic,
            Color = Color.Black,
            FontSize = 16f,
        };
        var box = CreateTextBox("italic", style);
        var paintList = Painter.Paint(box);

        var textCmd = paintList.Commands.OfType<DrawTextCommand>().First();
        Assert.Equal(FontStyleType.Italic, textCmd.FontStyle);
    }

    [Fact]
    public void DecorationColor_UsesCustomColor()
    {
        var style = new ComputedStyle
        {
            Display = DisplayType.Inline,
            TextDecorationLine = TextDecorationLine.Underline,
            TextDecorationColor = Color.FromName("red"),
            Color = Color.Black,
            FontSize = 16f,
        };
        var box = CreateTextBox("custom", style);
        var paintList = Painter.Paint(box);

        var rects = paintList.Commands.OfType<FillRectCommand>().ToList();
        Assert.Contains(rects, r => r.Color.R > 0 && r.Color.G == 0 && r.Color.B == 0);
    }
}
