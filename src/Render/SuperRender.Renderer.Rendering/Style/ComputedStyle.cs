using SuperRender.Document;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public enum TextAlign { Left, Right, Center, Justify }
public enum PositionType { Static, Relative, Absolute }
public enum FontStyleType { Normal, Italic, Oblique }
public enum BoxSizingType { ContentBox, BorderBox }
public enum OverflowType { Visible, Hidden, Scroll, Auto }
public enum TextOverflowType { Clip, Ellipsis }
public enum WhiteSpaceType { Normal, Pre, Nowrap, PreWrap, PreLine }

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

    // Box sizing and constraints
    public BoxSizingType BoxSizing { get; set; } = BoxSizingType.ContentBox;
    public float MinWidth { get; set; }
    public float MaxWidth { get; set; } = float.PositiveInfinity;
    public float MinHeight { get; set; }
    public float MaxHeight { get; set; } = float.PositiveInfinity;

    // Overflow
    public OverflowType Overflow { get; set; } = OverflowType.Visible;
    public TextOverflowType TextOverflow { get; set; } = TextOverflowType.Clip;

    // Colors
    public Color Color { get; set; } = Color.Black;
    public Color BackgroundColor { get; set; } = Color.Transparent;
    public Color BorderTopColor { get; set; } = Color.Black;
    public Color BorderRightColor { get; set; } = Color.Black;
    public Color BorderBottomColor { get; set; } = Color.Black;
    public Color BorderLeftColor { get; set; } = Color.Black;
    public string BorderTopStyle { get; set; } = "none";
    public string BorderRightStyle { get; set; } = "none";
    public string BorderBottomStyle { get; set; } = "none";
    public string BorderLeftStyle { get; set; } = "none";

    // Text
    public float FontSize { get; set; } = 16f;
    public IReadOnlyList<string> FontFamilies { get; set; } = ["sans-serif"];
    public string FontFamily
    {
        get => FontFamilies.Count > 0 ? FontFamilies[0] : "sans-serif";
        set => FontFamilies = [value];
    }
    public int FontWeight { get; set; } = 400;
    public FontStyleType FontStyle { get; set; } = FontStyleType.Normal;
    public TextAlign TextAlign { get; set; } = TextAlign.Left;
    public float LineHeight { get; set; } = 1.2f;
    public TextDecorationLine TextDecorationLine { get; set; } = TextDecorationLine.None;
    public Color? TextDecorationColor { get; set; }
    public WhiteSpaceType WhiteSpace { get; set; } = WhiteSpaceType.Normal;

    // Position
    public PositionType Position { get; set; } = PositionType.Static;
    public float Top { get; set; } = float.NaN;
    public float Left { get; set; } = float.NaN;
    public float Right { get; set; } = float.NaN;
    public float Bottom { get; set; } = float.NaN;
    public int ZIndex { get; set; }
    public bool ZIndexIsAuto { get; set; } = true;

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
            Overflow = Overflow,
            TextOverflow = TextOverflow,
            Color = Color,
            BackgroundColor = BackgroundColor,
            BorderTopColor = BorderTopColor,
            BorderRightColor = BorderRightColor,
            BorderBottomColor = BorderBottomColor,
            BorderLeftColor = BorderLeftColor,
            BorderTopStyle = BorderTopStyle,
            BorderRightStyle = BorderRightStyle,
            BorderBottomStyle = BorderBottomStyle,
            BorderLeftStyle = BorderLeftStyle,
            FontSize = FontSize,
            FontFamilies = FontFamilies,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            TextAlign = TextAlign,
            LineHeight = LineHeight,
            TextDecorationLine = TextDecorationLine,
            TextDecorationColor = TextDecorationColor,
            WhiteSpace = WhiteSpace,
            Position = Position,
            Top = Top,
            Left = Left,
            Right = Right,
            Bottom = Bottom,
            ZIndex = ZIndex,
            ZIndexIsAuto = ZIndexIsAuto,
        };
    }
}
