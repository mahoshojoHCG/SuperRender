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
    public const string Gap = "gap";
    public const string RowGap = "row-gap";
    public const string ColumnGap = "column-gap";

    // Pseudo-element
    public const string Content = "content";
}
