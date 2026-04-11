using SuperRender.Core.Layout;

namespace SuperRender.Core.Style;

public enum TextAlign { Left, Right, Center, Justify }
public enum PositionType { Static, Relative, Absolute }
public enum FontStyleType { Normal, Italic, Oblique }

[Flags]
public enum TextDecorationLine
{
    None = 0,
    Underline = 1,
    Overline = 2,
    LineThrough = 4,
}

public sealed class ComputedStyle
{
    public DisplayType Display { get; set; } = DisplayType.Block;

    // Box model (pixels after resolution)
    public float Width { get; set; } = float.NaN;   // NaN = auto
    public float Height { get; set; } = float.NaN;
    public EdgeSizes Margin { get; set; }
    public EdgeSizes Padding { get; set; }
    public EdgeSizes BorderWidth { get; set; }

    // Colors
    public Color Color { get; set; } = Color.Black;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BorderColor { get; set; } = Color.Black;
    public string BorderStyle { get; set; } = "none";

    // Text
    public float FontSize { get; set; } = 16f;
    public string FontFamily { get; set; } = "sans-serif";
    public int FontWeight { get; set; } = 400;
    public FontStyleType FontStyle { get; set; } = FontStyleType.Normal;
    public TextAlign TextAlign { get; set; } = TextAlign.Left;
    public float LineHeight { get; set; } = 1.2f;
    public TextDecorationLine TextDecorationLine { get; set; } = TextDecorationLine.None;
    public Color? TextDecorationColor { get; set; }

    // Position
    public PositionType Position { get; set; } = PositionType.Static;
    public float Top { get; set; } = float.NaN;
    public float Left { get; set; } = float.NaN;
    public float Right { get; set; } = float.NaN;
    public float Bottom { get; set; } = float.NaN;

    public ComputedStyle Clone()
    {
        return new ComputedStyle
        {
            Display = Display,
            Width = Width,
            Height = Height,
            Margin = Margin,
            Padding = Padding,
            BorderWidth = BorderWidth,
            Color = Color,
            BackgroundColor = BackgroundColor,
            BorderColor = BorderColor,
            BorderStyle = BorderStyle,
            FontSize = FontSize,
            FontFamily = FontFamily,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            TextAlign = TextAlign,
            LineHeight = LineHeight,
            TextDecorationLine = TextDecorationLine,
            TextDecorationColor = TextDecorationColor,
            Position = Position,
            Top = Top,
            Left = Left,
            Right = Right,
            Bottom = Bottom,
        };
    }
}
