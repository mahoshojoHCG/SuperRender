using SuperRender.Core.Layout;

namespace SuperRender.Core.Style;

public enum TextAlign { Left, Right, Center, Justify }
public enum PositionType { Static, Relative, Absolute }
public enum BoxSizing { ContentBox, BorderBox }
public enum OverflowType { Visible, Hidden, Scroll, Auto }
public enum TextOverflowType { Clip, Ellipsis }
public enum FontStyleType { Normal, Italic }

[Flags]
public enum TextDecorationLine
{
    None = 0,
    Underline = 1,
    LineThrough = 2,
    Overline = 4,
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

    // Box-sizing and min/max constraints
    public BoxSizing BoxSizing { get; set; } = BoxSizing.ContentBox;
    public float MinWidth { get; set; } = float.NaN;
    public float MaxWidth { get; set; } = float.NaN;
    public float MinHeight { get; set; } = float.NaN;
    public float MaxHeight { get; set; } = float.NaN;

    // Colors
    public Color Color { get; set; } = Color.Black;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BorderColor { get; set; } = Color.Black;
    public string BorderStyle { get; set; } = "none";

    // Text
    public float FontSize { get; set; } = 16f;
    public string FontFamily { get; set; } = "sans-serif";
    public TextAlign TextAlign { get; set; } = TextAlign.Left;
    public float LineHeight { get; set; } = 1.2f;
    public float FontWeight { get; set; } = 400f;
    public FontStyleType FontStyle { get; set; } = FontStyleType.Normal;
    public TextDecorationLine TextDecoration { get; set; } = TextDecorationLine.None;

    // Position
    public PositionType Position { get; set; } = PositionType.Static;
    public float Top { get; set; } = float.NaN;
    public float Left { get; set; } = float.NaN;
    public float Right { get; set; } = float.NaN;
    public float Bottom { get; set; } = float.NaN;
    public int ZIndex { get; set; }

    // Overflow
    public OverflowType Overflow { get; set; } = OverflowType.Visible;
    public TextOverflowType TextOverflow { get; set; } = TextOverflowType.Clip;

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
            BoxSizing = BoxSizing,
            MinWidth = MinWidth,
            MaxWidth = MaxWidth,
            MinHeight = MinHeight,
            MaxHeight = MaxHeight,
            Color = Color,
            BackgroundColor = BackgroundColor,
            BorderColor = BorderColor,
            BorderStyle = BorderStyle,
            FontSize = FontSize,
            FontFamily = FontFamily,
            TextAlign = TextAlign,
            LineHeight = LineHeight,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            TextDecoration = TextDecoration,
            Position = Position,
            Top = Top,
            Left = Left,
            Right = Right,
            Bottom = Bottom,
            ZIndex = ZIndex,
            Overflow = Overflow,
            TextOverflow = TextOverflow,
        };
    }
}
