namespace SuperRender.Renderer.Rendering.Style;

/// <summary>
/// Centralized constants for CSS property names used across the style system.
/// Eliminates magic string duplication between StyleResolver and PropertyDefaults.
/// </summary>
internal static class CssPropertyNames
{
    // Box model - sizing
    public const string Display = "display";
    public const string Width = "width";
    public const string Height = "height";
    public const string MinWidth = "min-width";
    public const string MaxWidth = "max-width";
    public const string MinHeight = "min-height";
    public const string MaxHeight = "max-height";
    public const string BoxSizing = "box-sizing";

    // Margin
    public const string MarginTop = "margin-top";
    public const string MarginRight = "margin-right";
    public const string MarginBottom = "margin-bottom";
    public const string MarginLeft = "margin-left";

    // Padding
    public const string PaddingTop = "padding-top";
    public const string PaddingRight = "padding-right";
    public const string PaddingBottom = "padding-bottom";
    public const string PaddingLeft = "padding-left";

    // Border - width
    public const string BorderWidth = "border-width";
    public const string BorderTopWidth = "border-top-width";
    public const string BorderRightWidth = "border-right-width";
    public const string BorderBottomWidth = "border-bottom-width";
    public const string BorderLeftWidth = "border-left-width";

    // Border - color
    public const string BorderColor = "border-color";
    public const string BorderTopColor = "border-top-color";
    public const string BorderRightColor = "border-right-color";
    public const string BorderBottomColor = "border-bottom-color";
    public const string BorderLeftColor = "border-left-color";

    // Border - style
    public const string BorderStyle = "border-style";
    public const string BorderTopStyle = "border-top-style";
    public const string BorderRightStyle = "border-right-style";
    public const string BorderBottomStyle = "border-bottom-style";
    public const string BorderLeftStyle = "border-left-style";

    // Border - radius
    public const string BorderTopLeftRadius = "border-top-left-radius";
    public const string BorderTopRightRadius = "border-top-right-radius";
    public const string BorderBottomRightRadius = "border-bottom-right-radius";
    public const string BorderBottomLeftRadius = "border-bottom-left-radius";

    // Color
    public const string Color = "color";
    public const string BackgroundColor = "background-color";

    // Background
    public const string Background = "background";
    public const string BackgroundImage = "background-image";
    public const string BackgroundRepeat = "background-repeat";
    public const string BackgroundPositionX = "background-position-x";
    public const string BackgroundPositionY = "background-position-y";
    public const string BackgroundSize = "background-size";
    public const string BackgroundAttachment = "background-attachment";
    public const string BackgroundOrigin = "background-origin";
    public const string BackgroundClip = "background-clip";

    // Box shadow
    public const string BoxShadow = "box-shadow";

    // Font
    public const string FontSize = "font-size";
    public const string FontFamily = "font-family";
    public const string FontWeight = "font-weight";
    public const string FontStyle = "font-style";

    // Text
    public const string TextAlign = "text-align";
    public const string LineHeight = "line-height";
    public const string TextDecoration = "text-decoration";
    public const string TextDecorationLine = "text-decoration-line";
    public const string TextDecorationColor = "text-decoration-color";
    public const string TextTransform = "text-transform";
    public const string TextOverflow = "text-overflow";
    public const string LetterSpacing = "letter-spacing";
    public const string WordSpacing = "word-spacing";
    public const string WhiteSpace = "white-space";
    public const string WordBreak = "word-break";
    public const string OverflowWrap = "overflow-wrap";
    public const string WordWrap = "word-wrap";

    // Layout
    public const string Position = "position";
    public const string Top = "top";
    public const string Left = "left";
    public const string Right = "right";
    public const string Bottom = "bottom";
    public const string ZIndex = "z-index";
    public const string Overflow = "overflow";
    public const string OverflowX = "overflow-x";
    public const string OverflowY = "overflow-y";

    // Visibility and opacity
    public const string Visibility = "visibility";
    public const string Opacity = "opacity";

    // Cursor
    public const string Cursor = "cursor";

    // List
    public const string ListStyleType = "list-style-type";
    public const string ListStyle = "list-style";

    // Flexbox
    public const string Flex = "flex";
    public const string FlexDirection = "flex-direction";
    public const string FlexWrap = "flex-wrap";
    public const string FlexFlow = "flex-flow";
    public const string FlexGrow = "flex-grow";
    public const string FlexShrink = "flex-shrink";
    public const string FlexBasis = "flex-basis";
    public const string JustifyContent = "justify-content";
    public const string AlignItems = "align-items";
    public const string AlignSelf = "align-self";
    public const string AlignContent = "align-content";
    public const string Gap = "gap";
    public const string RowGap = "row-gap";
    public const string ColumnGap = "column-gap";

    // Logical properties - margin
    public const string MarginBlockStart = "margin-block-start";
    public const string MarginBlockEnd = "margin-block-end";
    public const string MarginInlineStart = "margin-inline-start";
    public const string MarginInlineEnd = "margin-inline-end";

    // Logical properties - padding
    public const string PaddingBlockStart = "padding-block-start";
    public const string PaddingBlockEnd = "padding-block-end";
    public const string PaddingInlineStart = "padding-inline-start";
    public const string PaddingInlineEnd = "padding-inline-end";

    // Logical properties - sizing
    public const string InlineSize = "inline-size";
    public const string BlockSize = "block-size";
    public const string MinInlineSize = "min-inline-size";
    public const string MaxInlineSize = "max-inline-size";
    public const string MinBlockSize = "min-block-size";
    public const string MaxBlockSize = "max-block-size";

    // Place shorthands
    public const string PlaceItems = "place-items";
    public const string PlaceContent = "place-content";
    public const string PlaceSelf = "place-self";

    // Inset (position shorthands)
    public const string Inset = "inset";
    public const string InsetBlock = "inset-block";
    public const string InsetBlockStart = "inset-block-start";
    public const string InsetBlockEnd = "inset-block-end";
    public const string InsetInline = "inset-inline";
    public const string InsetInlineStart = "inset-inline-start";
    public const string InsetInlineEnd = "inset-inline-end";

    // Aspect ratio
    public const string AspectRatio = "aspect-ratio";

    // Pseudo-element
    public const string Content = "content";

    // Additional inherited properties
    public const string TextIndent = "text-indent";
    public const string TabSize = "tab-size";
    public const string FontVariant = "font-variant";
    public const string Direction = "direction";
    public const string Quotes = "quotes";

    // Transform
    public const string Transform = "transform";
    public const string TransformOrigin = "transform-origin";
    public const string TransformStyle = "transform-style";
    public const string Perspective = "perspective";
    public const string BackfaceVisibility = "backface-visibility";

    // Transitions
    public const string TransitionProperty = "transition-property";
    public const string TransitionDuration = "transition-duration";
    public const string TransitionTimingFunction = "transition-timing-function";
    public const string TransitionDelay = "transition-delay";
    public const string Transition = "transition";

    // Animations
    public const string AnimationName = "animation-name";
    public const string AnimationDuration = "animation-duration";
    public const string AnimationTimingFunction = "animation-timing-function";
    public const string AnimationDelay = "animation-delay";
    public const string AnimationIterationCount = "animation-iteration-count";
    public const string AnimationDirection = "animation-direction";
    public const string AnimationFillMode = "animation-fill-mode";
    public const string AnimationPlayState = "animation-play-state";
    public const string Animation = "animation";

    // Text enhancements
    public const string TextDecorationStyle = "text-decoration-style";
    public const string TextDecorationThickness = "text-decoration-thickness";
    public const string TextUnderlineOffset = "text-underline-offset";
    public const string TextShadow = "text-shadow";
    public const string VerticalAlign = "vertical-align";
    public const string ListStylePosition = "list-style-position";
    public const string CounterReset = "counter-reset";
    public const string CounterIncrement = "counter-increment";
    public const string Font = "font";
    public const string FontStretch = "font-stretch";

    // Grid
    public const string GridTemplateRows = "grid-template-rows";
    public const string GridTemplateColumns = "grid-template-columns";
    public const string GridTemplateAreas = "grid-template-areas";
    public const string GridTemplate = "grid-template";
    public const string GridRowStart = "grid-row-start";
    public const string GridRowEnd = "grid-row-end";
    public const string GridColumnStart = "grid-column-start";
    public const string GridColumnEnd = "grid-column-end";
    public const string GridRow = "grid-row";
    public const string GridColumn = "grid-column";
    public const string GridArea = "grid-area";
    public const string GridAutoRows = "grid-auto-rows";
    public const string GridAutoColumns = "grid-auto-columns";
    public const string GridAutoFlow = "grid-auto-flow";

    // Float and clear
    public const string Float = "float";
    public const string Clear = "clear";

    // Table
    public const string TableLayout = "table-layout";
    public const string BorderCollapse = "border-collapse";
    public const string BorderSpacing = "border-spacing";
    public const string CaptionSide = "caption-side";
    public const string EmptyCells = "empty-cells";

    // Visual properties
    public const string PointerEvents = "pointer-events";
    public const string UserSelect = "user-select";
    public const string ObjectFit = "object-fit";
    public const string ObjectPosition = "object-position";
    public const string WillChange = "will-change";
    public const string ColorScheme = "color-scheme";
    public const string Appearance = "appearance";
    public const string Outline = "outline";
    public const string OutlineColor = "outline-color";
    public const string OutlineStyle = "outline-style";
    public const string OutlineWidth = "outline-width";
    public const string OutlineOffset = "outline-offset";
    public const string Resize = "resize";

    // Filters and compositing
    public const string Filter = "filter";
    public const string BackdropFilter = "backdrop-filter";
    public const string MixBlendMode = "mix-blend-mode";
    public const string Isolation = "isolation";
    public const string ClipPath = "clip-path";

    // Container queries
    public const string ContainerType = "container-type";
    public const string ContainerName = "container-name";

    // Writing modes
    public const string WritingMode = "writing-mode";
    public const string TextOrientation = "text-orientation";

    // Scroll snap
    public const string ScrollSnapType = "scroll-snap-type";
    public const string ScrollSnapAlign = "scroll-snap-align";
}
