using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public enum TextAlign { Left, Right, Center, Justify }
public enum PositionType { Static, Relative, Absolute }
public enum FontStyleType { Normal, Italic, Oblique }
public enum BoxSizingType { ContentBox, BorderBox }
public enum OverflowType { Visible, Hidden, Scroll, Auto }
public enum TextOverflowType { Clip, Ellipsis }
public enum WhiteSpaceType { Normal, Pre, Nowrap, PreWrap, PreLine }
public enum VisibilityType { Visible, Hidden, Collapse }
public enum TextTransformType { None, Uppercase, Lowercase, Capitalize }
public enum CursorType { Auto, Default, Pointer, Text, Crosshair, Move, NotAllowed, Wait, Help }
public enum WordBreakType { Normal, BreakAll, KeepAll }
public enum OverflowWrapType { Normal, BreakWord, Anywhere }
public enum FlexDirectionType { Row, RowReverse, Column, ColumnReverse }
public enum FlexWrapType { Nowrap, Wrap, WrapReverse }
public enum JustifyContentType { FlexStart, FlexEnd, Center, SpaceBetween, SpaceAround, SpaceEvenly }
public enum AlignItemsType { Stretch, FlexStart, FlexEnd, Center, Baseline }
public enum AlignSelfType { Auto, Stretch, FlexStart, FlexEnd, Center, Baseline }

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
    public CalcNode? WidthCalc { get; set; }  // Deferred calc/percentage expression for width
    public CalcNode? HeightCalc { get; set; } // Deferred calc/percentage expression for height
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

    // Border radius
    public float BorderTopLeftRadius { get; set; }
    public float BorderTopRightRadius { get; set; }
    public float BorderBottomRightRadius { get; set; }
    public float BorderBottomLeftRadius { get; set; }

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

    // P1: Additional inherited properties
    public VisibilityType Visibility { get; set; } = VisibilityType.Visible;
    public TextTransformType TextTransform { get; set; } = TextTransformType.None;
    public float LetterSpacing { get; set; }
    public float WordSpacing { get; set; }
    public CursorType Cursor { get; set; } = CursorType.Auto;
    public WordBreakType WordBreak { get; set; } = WordBreakType.Normal;
    public OverflowWrapType OverflowWrap { get; set; } = OverflowWrapType.Normal;
    public string ListStyleType { get; set; } = "disc";

    // P1: Opacity
    public float Opacity { get; set; } = 1f;

    // Flexbox
    public FlexDirectionType FlexDirection { get; set; } = FlexDirectionType.Row;
    public FlexWrapType FlexWrap { get; set; } = FlexWrapType.Nowrap;
    public JustifyContentType JustifyContent { get; set; } = JustifyContentType.FlexStart;
    public AlignItemsType AlignItems { get; set; } = AlignItemsType.Stretch;
    public AlignSelfType AlignSelf { get; set; } = AlignSelfType.Auto;
    public float FlexGrow { get; set; }
    public float FlexShrink { get; set; } = 1f;
    public float FlexBasis { get; set; } = float.NaN; // NaN = auto
    public float Gap { get; set; }
    public float RowGap { get; set; } = float.NaN; // NaN = use Gap
    public float ColumnGap { get; set; } = float.NaN; // NaN = use Gap

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
            WidthCalc = WidthCalc,
            HeightCalc = HeightCalc,
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
            BorderTopLeftRadius = BorderTopLeftRadius,
            BorderTopRightRadius = BorderTopRightRadius,
            BorderBottomRightRadius = BorderBottomRightRadius,
            BorderBottomLeftRadius = BorderBottomLeftRadius,
            FontSize = FontSize,
            FontFamilies = FontFamilies,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            TextAlign = TextAlign,
            LineHeight = LineHeight,
            TextDecorationLine = TextDecorationLine,
            TextDecorationColor = TextDecorationColor,
            WhiteSpace = WhiteSpace,
            Visibility = Visibility,
            TextTransform = TextTransform,
            LetterSpacing = LetterSpacing,
            WordSpacing = WordSpacing,
            Cursor = Cursor,
            WordBreak = WordBreak,
            OverflowWrap = OverflowWrap,
            ListStyleType = ListStyleType,
            Opacity = Opacity,
            FlexDirection = FlexDirection,
            FlexWrap = FlexWrap,
            JustifyContent = JustifyContent,
            AlignItems = AlignItems,
            AlignSelf = AlignSelf,
            FlexGrow = FlexGrow,
            FlexShrink = FlexShrink,
            FlexBasis = FlexBasis,
            Gap = Gap,
            RowGap = RowGap,
            ColumnGap = ColumnGap,
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
