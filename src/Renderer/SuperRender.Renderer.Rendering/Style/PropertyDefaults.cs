namespace SuperRender.Renderer.Rendering.Style;

public static class PropertyDefaults
{
    public static readonly HashSet<string> InheritedProperties =
    [
        CssPropertyNames.Color,
        CssPropertyNames.FontSize,
        CssPropertyNames.FontFamily,
        CssPropertyNames.FontWeight,
        CssPropertyNames.FontStyle,
        CssPropertyNames.TextAlign,
        CssPropertyNames.LineHeight,
        CssPropertyNames.WhiteSpace,
        CssPropertyNames.Visibility,
        CssPropertyNames.LetterSpacing,
        CssPropertyNames.WordSpacing,
        CssPropertyNames.Cursor,
        CssPropertyNames.TextTransform,
        CssPropertyNames.WordBreak,
        CssPropertyNames.OverflowWrap,
        CssPropertyNames.ListStyleType,
    ];

    public static bool IsInherited(string property) => InheritedProperties.Contains(property);

    /// <summary>
    /// Resets the given property to its CSS initial value on the style object.
    /// </summary>
    public static void ApplyInitialValue(ComputedStyle style, string property)
    {
        switch (property)
        {
            case CssPropertyNames.Display: style.Display = Layout.DisplayType.Block; break;
            case CssPropertyNames.Width: style.Width = float.NaN; break;
            case CssPropertyNames.Height: style.Height = float.NaN; break;
            case CssPropertyNames.MarginTop: style.Margin = style.Margin with { Top = 0 }; break;
            case CssPropertyNames.MarginRight: style.Margin = style.Margin with { Right = 0 }; break;
            case CssPropertyNames.MarginBottom: style.Margin = style.Margin with { Bottom = 0 }; break;
            case CssPropertyNames.MarginLeft: style.Margin = style.Margin with { Left = 0 }; break;
            case CssPropertyNames.PaddingTop: style.Padding = style.Padding with { Top = 0 }; break;
            case CssPropertyNames.PaddingRight: style.Padding = style.Padding with { Right = 0 }; break;
            case CssPropertyNames.PaddingBottom: style.Padding = style.Padding with { Bottom = 0 }; break;
            case CssPropertyNames.PaddingLeft: style.Padding = style.Padding with { Left = 0 }; break;
            case CssPropertyNames.BorderTopWidth: style.BorderWidth = style.BorderWidth with { Top = 0 }; break;
            case CssPropertyNames.BorderRightWidth: style.BorderWidth = style.BorderWidth with { Right = 0 }; break;
            case CssPropertyNames.BorderBottomWidth: style.BorderWidth = style.BorderWidth with { Bottom = 0 }; break;
            case CssPropertyNames.BorderLeftWidth: style.BorderWidth = style.BorderWidth with { Left = 0 }; break;
            case CssPropertyNames.Color: style.Color = Document.Color.Black; break;
            case CssPropertyNames.BackgroundColor: style.BackgroundColor = Document.Color.Transparent; break;
            case CssPropertyNames.FontSize: style.FontSize = 16f; break;
            case CssPropertyNames.FontFamily: style.FontFamilies = ["sans-serif"]; break;
            case CssPropertyNames.FontWeight: style.FontWeight = 400; break;
            case CssPropertyNames.FontStyle: style.FontStyle = FontStyleType.Normal; break;
            case CssPropertyNames.TextAlign: style.TextAlign = TextAlign.Left; break;
            case CssPropertyNames.LineHeight: style.LineHeight = 1.2f; break;
            case CssPropertyNames.WhiteSpace: style.WhiteSpace = WhiteSpaceType.Normal; break;
            case CssPropertyNames.Position: style.Position = PositionType.Static; break;
            case CssPropertyNames.Top: style.Top = float.NaN; break;
            case CssPropertyNames.Left: style.Left = float.NaN; break;
            case CssPropertyNames.Right: style.Right = float.NaN; break;
            case CssPropertyNames.Bottom: style.Bottom = float.NaN; break;
            case CssPropertyNames.ZIndex: style.ZIndex = 0; style.ZIndexIsAuto = true; break;
            case CssPropertyNames.Overflow or CssPropertyNames.OverflowX or CssPropertyNames.OverflowY: style.Overflow = OverflowType.Visible; break;
            case CssPropertyNames.TextOverflow: style.TextOverflow = TextOverflowType.Clip; break;
            case CssPropertyNames.BoxSizing: style.BoxSizing = BoxSizingType.ContentBox; break;
            case CssPropertyNames.MinWidth: style.MinWidth = 0; break;
            case CssPropertyNames.MaxWidth: style.MaxWidth = float.PositiveInfinity; break;
            case CssPropertyNames.MinHeight: style.MinHeight = 0; break;
            case CssPropertyNames.MaxHeight: style.MaxHeight = float.PositiveInfinity; break;
            case CssPropertyNames.Visibility: style.Visibility = VisibilityType.Visible; break;
            case CssPropertyNames.TextTransform: style.TextTransform = TextTransformType.None; break;
            case CssPropertyNames.LetterSpacing: style.LetterSpacing = 0; break;
            case CssPropertyNames.WordSpacing: style.WordSpacing = 0; break;
            case CssPropertyNames.Cursor: style.Cursor = CursorType.Auto; break;
            case CssPropertyNames.WordBreak: style.WordBreak = WordBreakType.Normal; break;
            case CssPropertyNames.OverflowWrap or CssPropertyNames.WordWrap: style.OverflowWrap = OverflowWrapType.Normal; break;
            case CssPropertyNames.ListStyleType: style.ListStyleType = "disc"; break;
            case CssPropertyNames.Opacity: style.Opacity = 1f; break;
            case CssPropertyNames.TextDecoration or CssPropertyNames.TextDecorationLine: style.TextDecorationLine = TextDecorationLine.None; break;
            case CssPropertyNames.TextDecorationColor: style.TextDecorationColor = null; break;
            case CssPropertyNames.BorderColor: style.BorderTopColor = style.BorderRightColor = style.BorderBottomColor = style.BorderLeftColor = Document.Color.Black; break;
            case CssPropertyNames.BorderStyle: style.BorderTopStyle = style.BorderRightStyle = style.BorderBottomStyle = style.BorderLeftStyle = "none"; break;
            case CssPropertyNames.FlexDirection: style.FlexDirection = FlexDirectionType.Row; break;
            case CssPropertyNames.FlexWrap: style.FlexWrap = FlexWrapType.Nowrap; break;
            case CssPropertyNames.JustifyContent: style.JustifyContent = JustifyContentType.FlexStart; break;
            case CssPropertyNames.AlignItems: style.AlignItems = AlignItemsType.Stretch; break;
            case CssPropertyNames.AlignSelf: style.AlignSelf = AlignSelfType.Auto; break;
            case CssPropertyNames.FlexGrow: style.FlexGrow = 0; break;
            case CssPropertyNames.FlexShrink: style.FlexShrink = 1; break;
            case CssPropertyNames.FlexBasis: style.FlexBasis = float.NaN; break;
            case CssPropertyNames.Gap: style.Gap = 0; break;
            case CssPropertyNames.RowGap: style.RowGap = float.NaN; break;
            case CssPropertyNames.ColumnGap: style.ColumnGap = float.NaN; break;
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
            case CssPropertyNames.Color: target.Color = source.Color; break;
            case CssPropertyNames.FontSize: target.FontSize = source.FontSize; break;
            case CssPropertyNames.FontFamily: target.FontFamilies = source.FontFamilies; break;
            case CssPropertyNames.FontWeight: target.FontWeight = source.FontWeight; break;
            case CssPropertyNames.FontStyle: target.FontStyle = source.FontStyle; break;
            case CssPropertyNames.TextAlign: target.TextAlign = source.TextAlign; break;
            case CssPropertyNames.LineHeight: target.LineHeight = source.LineHeight; break;
            case CssPropertyNames.WhiteSpace: target.WhiteSpace = source.WhiteSpace; break;
            case CssPropertyNames.Visibility: target.Visibility = source.Visibility; break;
            case CssPropertyNames.TextTransform: target.TextTransform = source.TextTransform; break;
            case CssPropertyNames.LetterSpacing: target.LetterSpacing = source.LetterSpacing; break;
            case CssPropertyNames.WordSpacing: target.WordSpacing = source.WordSpacing; break;
            case CssPropertyNames.Cursor: target.Cursor = source.Cursor; break;
            case CssPropertyNames.WordBreak: target.WordBreak = source.WordBreak; break;
            case CssPropertyNames.OverflowWrap or CssPropertyNames.WordWrap: target.OverflowWrap = source.OverflowWrap; break;
            case CssPropertyNames.ListStyleType: target.ListStyleType = source.ListStyleType; break;
            case CssPropertyNames.Display: target.Display = source.Display; break;
            case CssPropertyNames.Width: target.Width = source.Width; break;
            case CssPropertyNames.Height: target.Height = source.Height; break;
            case CssPropertyNames.BackgroundColor: target.BackgroundColor = source.BackgroundColor; break;
            case CssPropertyNames.Opacity: target.Opacity = source.Opacity; break;
            case CssPropertyNames.Position: target.Position = source.Position; break;
            case CssPropertyNames.Overflow or CssPropertyNames.OverflowX or CssPropertyNames.OverflowY: target.Overflow = source.Overflow; break;
            case CssPropertyNames.BoxSizing: target.BoxSizing = source.BoxSizing; break;
            case CssPropertyNames.FlexDirection: target.FlexDirection = source.FlexDirection; break;
            case CssPropertyNames.FlexWrap: target.FlexWrap = source.FlexWrap; break;
            case CssPropertyNames.JustifyContent: target.JustifyContent = source.JustifyContent; break;
            case CssPropertyNames.AlignItems: target.AlignItems = source.AlignItems; break;
            case CssPropertyNames.AlignSelf: target.AlignSelf = source.AlignSelf; break;
            case CssPropertyNames.FlexGrow: target.FlexGrow = source.FlexGrow; break;
            case CssPropertyNames.FlexShrink: target.FlexShrink = source.FlexShrink; break;
            case CssPropertyNames.FlexBasis: target.FlexBasis = source.FlexBasis; break;
            case CssPropertyNames.Gap: target.Gap = source.Gap; break;
            case CssPropertyNames.RowGap: target.RowGap = source.RowGap; break;
            case CssPropertyNames.ColumnGap: target.ColumnGap = source.ColumnGap; break;
        }
    }
}
