using SuperRender.Document;
using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public enum TextAlign { Left, Right, Center, Justify }
public enum PositionType { Static, Relative, Absolute, Fixed, Sticky }
public enum FontStyleType { Normal, Italic, Oblique }
public enum BoxSizingType { ContentBox, BorderBox }
public enum OverflowType { Visible, Hidden, Scroll, Auto, Clip }
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
public enum AlignContentType { Stretch, FlexStart, FlexEnd, Center, SpaceBetween, SpaceAround, SpaceEvenly }

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
    public OverflowType OverflowX { get; set; } = OverflowType.Visible;
    public OverflowType OverflowY { get; set; } = OverflowType.Visible;
    public TextOverflowType TextOverflow { get; set; } = TextOverflowType.Clip;

    // Colors
    public Color Color { get; set; } = Color.Black;
    public Color BackgroundColor { get; set; } = Color.Transparent;

    // Background image / gradient
    public CssGradient? BackgroundImage { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string BackgroundRepeat { get; set; } = "repeat";
    public float BackgroundPositionX { get; set; }
    public float BackgroundPositionY { get; set; }
    public string BackgroundSize { get; set; } = "auto";
    public string BackgroundAttachment { get; set; } = "scroll";
    public string BackgroundOrigin { get; set; } = "padding-box";
    public string BackgroundClip { get; set; } = "border-box";

    // Box shadow
    public List<BoxShadowDescriptor>? BoxShadows { get; set; }

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

    // Additional inherited properties
    public float TextIndent { get; set; }
    public float TabSize { get; set; } = 8f;
    public string FontVariant { get; set; } = "normal";
    public string Direction { get; set; } = "ltr";
    public string Quotes { get; set; } = "";

    // Flexbox
    public FlexDirectionType FlexDirection { get; set; } = FlexDirectionType.Row;
    public FlexWrapType FlexWrap { get; set; } = FlexWrapType.Nowrap;
    public JustifyContentType JustifyContent { get; set; } = JustifyContentType.FlexStart;
    public AlignItemsType AlignItems { get; set; } = AlignItemsType.Stretch;
    public AlignSelfType AlignSelf { get; set; } = AlignSelfType.Auto;
    public AlignContentType AlignContent { get; set; } = AlignContentType.Stretch;
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

    // Aspect ratio
    public float AspectRatio { get; set; } = float.NaN; // NaN = auto/none

    // Transform
    public List<TransformFunction>? Transform { get; set; }
    public float TransformOriginX { get; set; } = 50f; // percentage, default center
    public float TransformOriginY { get; set; } = 50f;
    public string TransformStyle { get; set; } = "flat";
    public float Perspective { get; set; } = float.NaN; // NaN = none
    public string BackfaceVisibility { get; set; } = "visible";

    // Transitions (Phase 10)
    public string? TransitionProperty { get; set; }
    public float TransitionDuration { get; set; }
    public TimingFunction? TransitionTimingFunction { get; set; }
    public float TransitionDelay { get; set; }

    // Animations (Phase 10)
    public string? AnimationName { get; set; }
    public float AnimationDuration { get; set; }
    public TimingFunction? AnimationTimingFunction { get; set; }
    public float AnimationDelay { get; set; }
    public float AnimationIterationCount { get; set; } = 1f;
    public string AnimationDirection { get; set; } = "normal";
    public string AnimationFillMode { get; set; } = "none";
    public string AnimationPlayState { get; set; } = "running";

    // Text enhancements (Phase 11)
    public string TextDecorationStyle { get; set; } = "solid";
    public float TextDecorationThickness { get; set; } = float.NaN; // NaN = auto
    public float TextUnderlineOffset { get; set; } = float.NaN; // NaN = auto
    public string? TextShadow { get; set; }
    public List<BoxShadowDescriptor>? TextShadows { get; set; }
    public string VerticalAlign { get; set; } = "baseline";
    public float VerticalAlignLength { get; set; }
    public string ListStylePosition { get; set; } = "outside";
    public string? CounterReset { get; set; }
    public string? CounterIncrement { get; set; }
    public string FontStretch { get; set; } = "normal";

    // Grid (Phase 12)
    public string? GridTemplateRows { get; set; }
    public string? GridTemplateColumns { get; set; }
    public string? GridTemplateAreas { get; set; }
    public string? GridRowStart { get; set; }
    public string? GridRowEnd { get; set; }
    public string? GridColumnStart { get; set; }
    public string? GridColumnEnd { get; set; }
    public string? GridAutoRows { get; set; }
    public string? GridAutoColumns { get; set; }
    public string GridAutoFlow { get; set; } = "row";

    // Float and clear (Phase 13)
    public string Float { get; set; } = "none";
    public string Clear { get; set; } = "none";

    // Table (Phase 13)
    public string TableLayout { get; set; } = "auto";
    public string BorderCollapse { get; set; } = "separate";
    public float BorderSpacing { get; set; }

    // Visual properties (Phase 13)
    public string PointerEvents { get; set; } = "auto";
    public string UserSelect { get; set; } = "auto";
    public string ObjectFit { get; set; } = "fill";
    public string? ObjectPosition { get; set; }
    public string? WillChange { get; set; }
    public string? ColorScheme { get; set; }
    public string Appearance { get; set; } = "auto";
    public float OutlineWidth { get; set; }
    public string OutlineStyle { get; set; } = "none";
    public Color OutlineColor { get; set; } = Color.Black;
    public float OutlineOffset { get; set; }

    // Filters and compositing (Phase 14)
    public List<FilterFunction>? Filter { get; set; }
    public List<FilterFunction>? BackdropFilter { get; set; }
    public string MixBlendMode { get; set; } = "normal";
    public string Isolation { get; set; } = "auto";
    public string? ClipPath { get; set; }

    // Container queries (Phase 14)
    public string ContainerType { get; set; } = "normal";
    public string? ContainerName { get; set; }

    // Writing modes (Phase 14)
    public string WritingMode { get; set; } = "horizontal-tb";
    public string TextOrientation { get; set; } = "mixed";

    // Scroll snap (Phase 14)
    public string? ScrollSnapType { get; set; }
    public string? ScrollSnapAlign { get; set; }

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
            OverflowX = OverflowX,
            OverflowY = OverflowY,
            TextOverflow = TextOverflow,
            Color = Color,
            BackgroundColor = BackgroundColor,
            BackgroundImage = BackgroundImage,
            BackgroundImageUrl = BackgroundImageUrl,
            BackgroundRepeat = BackgroundRepeat,
            BackgroundPositionX = BackgroundPositionX,
            BackgroundPositionY = BackgroundPositionY,
            BackgroundSize = BackgroundSize,
            BackgroundAttachment = BackgroundAttachment,
            BackgroundOrigin = BackgroundOrigin,
            BackgroundClip = BackgroundClip,
            BoxShadows = BoxShadows,
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
            TextIndent = TextIndent,
            TabSize = TabSize,
            FontVariant = FontVariant,
            Direction = Direction,
            Quotes = Quotes,
            FlexDirection = FlexDirection,
            FlexWrap = FlexWrap,
            JustifyContent = JustifyContent,
            AlignItems = AlignItems,
            AlignSelf = AlignSelf,
            AlignContent = AlignContent,
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
            AspectRatio = AspectRatio,
            Transform = Transform,
            TransformOriginX = TransformOriginX,
            TransformOriginY = TransformOriginY,
            TransformStyle = TransformStyle,
            Perspective = Perspective,
            BackfaceVisibility = BackfaceVisibility,
            TransitionProperty = TransitionProperty,
            TransitionDuration = TransitionDuration,
            TransitionTimingFunction = TransitionTimingFunction,
            TransitionDelay = TransitionDelay,
            AnimationName = AnimationName,
            AnimationDuration = AnimationDuration,
            AnimationTimingFunction = AnimationTimingFunction,
            AnimationDelay = AnimationDelay,
            AnimationIterationCount = AnimationIterationCount,
            AnimationDirection = AnimationDirection,
            AnimationFillMode = AnimationFillMode,
            AnimationPlayState = AnimationPlayState,
            TextDecorationStyle = TextDecorationStyle,
            TextDecorationThickness = TextDecorationThickness,
            TextUnderlineOffset = TextUnderlineOffset,
            TextShadow = TextShadow,
            TextShadows = TextShadows,
            VerticalAlign = VerticalAlign,
            VerticalAlignLength = VerticalAlignLength,
            ListStylePosition = ListStylePosition,
            CounterReset = CounterReset,
            CounterIncrement = CounterIncrement,
            FontStretch = FontStretch,
            GridTemplateRows = GridTemplateRows,
            GridTemplateColumns = GridTemplateColumns,
            GridTemplateAreas = GridTemplateAreas,
            GridRowStart = GridRowStart,
            GridRowEnd = GridRowEnd,
            GridColumnStart = GridColumnStart,
            GridColumnEnd = GridColumnEnd,
            GridAutoRows = GridAutoRows,
            GridAutoColumns = GridAutoColumns,
            GridAutoFlow = GridAutoFlow,
            Float = Float,
            Clear = Clear,
            TableLayout = TableLayout,
            BorderCollapse = BorderCollapse,
            BorderSpacing = BorderSpacing,
            PointerEvents = PointerEvents,
            UserSelect = UserSelect,
            ObjectFit = ObjectFit,
            ObjectPosition = ObjectPosition,
            WillChange = WillChange,
            ColorScheme = ColorScheme,
            Appearance = Appearance,
            OutlineWidth = OutlineWidth,
            OutlineStyle = OutlineStyle,
            OutlineColor = OutlineColor,
            OutlineOffset = OutlineOffset,
            Filter = Filter,
            BackdropFilter = BackdropFilter,
            MixBlendMode = MixBlendMode,
            Isolation = Isolation,
            ClipPath = ClipPath,
            ContainerType = ContainerType,
            ContainerName = ContainerName,
            WritingMode = WritingMode,
            TextOrientation = TextOrientation,
            ScrollSnapType = ScrollSnapType,
            ScrollSnapAlign = ScrollSnapAlign,
        };
    }
}
