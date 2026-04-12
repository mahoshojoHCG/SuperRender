namespace SuperRender.Renderer.Rendering.Style;

public static class PropertyDefaults
{
    public static readonly HashSet<string> InheritedProperties =
    [
        "color",
        "font-size",
        "font-family",
        "font-weight",
        "font-style",
        "text-align",
        "line-height",
        "white-space",
        "visibility",
        "letter-spacing",
        "word-spacing",
        "cursor",
        "text-transform",
        "word-break",
        "overflow-wrap",
        "list-style-type",
    ];

    public static bool IsInherited(string property) => InheritedProperties.Contains(property);

    /// <summary>
    /// Resets the given property to its CSS initial value on the style object.
    /// </summary>
    public static void ApplyInitialValue(ComputedStyle style, string property)
    {
        switch (property)
        {
            case "display": style.Display = Layout.DisplayType.Block; break;
            case "width": style.Width = float.NaN; break;
            case "height": style.Height = float.NaN; break;
            case "margin-top": style.Margin = style.Margin with { Top = 0 }; break;
            case "margin-right": style.Margin = style.Margin with { Right = 0 }; break;
            case "margin-bottom": style.Margin = style.Margin with { Bottom = 0 }; break;
            case "margin-left": style.Margin = style.Margin with { Left = 0 }; break;
            case "padding-top": style.Padding = style.Padding with { Top = 0 }; break;
            case "padding-right": style.Padding = style.Padding with { Right = 0 }; break;
            case "padding-bottom": style.Padding = style.Padding with { Bottom = 0 }; break;
            case "padding-left": style.Padding = style.Padding with { Left = 0 }; break;
            case "border-top-width": style.BorderWidth = style.BorderWidth with { Top = 0 }; break;
            case "border-right-width": style.BorderWidth = style.BorderWidth with { Right = 0 }; break;
            case "border-bottom-width": style.BorderWidth = style.BorderWidth with { Bottom = 0 }; break;
            case "border-left-width": style.BorderWidth = style.BorderWidth with { Left = 0 }; break;
            case "color": style.Color = Document.Color.Black; break;
            case "background-color": style.BackgroundColor = Document.Color.Transparent; break;
            case "font-size": style.FontSize = 16f; break;
            case "font-family": style.FontFamilies = ["sans-serif"]; break;
            case "font-weight": style.FontWeight = 400; break;
            case "font-style": style.FontStyle = FontStyleType.Normal; break;
            case "text-align": style.TextAlign = TextAlign.Left; break;
            case "line-height": style.LineHeight = 1.2f; break;
            case "white-space": style.WhiteSpace = WhiteSpaceType.Normal; break;
            case "position": style.Position = PositionType.Static; break;
            case "top": style.Top = float.NaN; break;
            case "left": style.Left = float.NaN; break;
            case "right": style.Right = float.NaN; break;
            case "bottom": style.Bottom = float.NaN; break;
            case "z-index": style.ZIndex = 0; style.ZIndexIsAuto = true; break;
            case "overflow" or "overflow-x" or "overflow-y": style.Overflow = OverflowType.Visible; break;
            case "text-overflow": style.TextOverflow = TextOverflowType.Clip; break;
            case "box-sizing": style.BoxSizing = BoxSizingType.ContentBox; break;
            case "min-width": style.MinWidth = 0; break;
            case "max-width": style.MaxWidth = float.PositiveInfinity; break;
            case "min-height": style.MinHeight = 0; break;
            case "max-height": style.MaxHeight = float.PositiveInfinity; break;
            case "visibility": style.Visibility = VisibilityType.Visible; break;
            case "text-transform": style.TextTransform = TextTransformType.None; break;
            case "letter-spacing": style.LetterSpacing = 0; break;
            case "word-spacing": style.WordSpacing = 0; break;
            case "cursor": style.Cursor = CursorType.Auto; break;
            case "word-break": style.WordBreak = WordBreakType.Normal; break;
            case "overflow-wrap" or "word-wrap": style.OverflowWrap = OverflowWrapType.Normal; break;
            case "list-style-type": style.ListStyleType = "disc"; break;
            case "opacity": style.Opacity = 1f; break;
            case "text-decoration" or "text-decoration-line": style.TextDecorationLine = TextDecorationLine.None; break;
            case "text-decoration-color": style.TextDecorationColor = null; break;
            case "border-color": style.BorderTopColor = style.BorderRightColor = style.BorderBottomColor = style.BorderLeftColor = Document.Color.Black; break;
            case "border-style": style.BorderTopStyle = style.BorderRightStyle = style.BorderBottomStyle = style.BorderLeftStyle = "none"; break;
            case "flex-direction": style.FlexDirection = FlexDirectionType.Row; break;
            case "flex-wrap": style.FlexWrap = FlexWrapType.Nowrap; break;
            case "justify-content": style.JustifyContent = JustifyContentType.FlexStart; break;
            case "align-items": style.AlignItems = AlignItemsType.Stretch; break;
            case "align-self": style.AlignSelf = AlignSelfType.Auto; break;
            case "flex-grow": style.FlexGrow = 0; break;
            case "flex-shrink": style.FlexShrink = 1; break;
            case "flex-basis": style.FlexBasis = float.NaN; break;
            case "gap": style.Gap = 0; break;
            case "row-gap": style.RowGap = float.NaN; break;
            case "column-gap": style.ColumnGap = float.NaN; break;
        }
    }

    /// <summary>
    /// Copies the value of the given property from source to target.
    /// Used for 'inherit' keyword.
    /// </summary>
    public static void InheritProperty(ComputedStyle target, string property, ComputedStyle source)
    {
        switch (property)
        {
            case "color": target.Color = source.Color; break;
            case "font-size": target.FontSize = source.FontSize; break;
            case "font-family": target.FontFamilies = source.FontFamilies; break;
            case "font-weight": target.FontWeight = source.FontWeight; break;
            case "font-style": target.FontStyle = source.FontStyle; break;
            case "text-align": target.TextAlign = source.TextAlign; break;
            case "line-height": target.LineHeight = source.LineHeight; break;
            case "white-space": target.WhiteSpace = source.WhiteSpace; break;
            case "visibility": target.Visibility = source.Visibility; break;
            case "text-transform": target.TextTransform = source.TextTransform; break;
            case "letter-spacing": target.LetterSpacing = source.LetterSpacing; break;
            case "word-spacing": target.WordSpacing = source.WordSpacing; break;
            case "cursor": target.Cursor = source.Cursor; break;
            case "word-break": target.WordBreak = source.WordBreak; break;
            case "overflow-wrap" or "word-wrap": target.OverflowWrap = source.OverflowWrap; break;
            case "list-style-type": target.ListStyleType = source.ListStyleType; break;
            case "display": target.Display = source.Display; break;
            case "width": target.Width = source.Width; break;
            case "height": target.Height = source.Height; break;
            case "background-color": target.BackgroundColor = source.BackgroundColor; break;
            case "opacity": target.Opacity = source.Opacity; break;
            case "position": target.Position = source.Position; break;
            case "overflow" or "overflow-x" or "overflow-y": target.Overflow = source.Overflow; break;
            case "box-sizing": target.BoxSizing = source.BoxSizing; break;
            case "flex-direction": target.FlexDirection = source.FlexDirection; break;
            case "flex-wrap": target.FlexWrap = source.FlexWrap; break;
            case "justify-content": target.JustifyContent = source.JustifyContent; break;
            case "align-items": target.AlignItems = source.AlignItems; break;
            case "align-self": target.AlignSelf = source.AlignSelf; break;
            case "flex-grow": target.FlexGrow = source.FlexGrow; break;
            case "flex-shrink": target.FlexShrink = source.FlexShrink; break;
            case "flex-basis": target.FlexBasis = source.FlexBasis; break;
            case "gap": target.Gap = source.Gap; break;
            case "row-gap": target.RowGap = source.RowGap; break;
            case "column-gap": target.ColumnGap = source.ColumnGap; break;
        }
    }
}
