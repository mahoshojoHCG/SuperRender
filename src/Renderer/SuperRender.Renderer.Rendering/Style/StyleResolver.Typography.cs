using SuperRender.Document.Style;
using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyTypographyProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.FontSize:
                style.FontSize = ResolveFontSize(value, parentStyle);
                break;
            case CssPropertyNames.FontFamily:
                style.FontFamilies = FontFamilyParser.Parse(value.Raw);
                break;

            case CssPropertyNames.TextAlign:
                style.TextAlign = value.Raw.ToLowerInvariant() switch
                {
                    "left" => TextAlign.Left,
                    "right" => TextAlign.Right,
                    "center" => TextAlign.Center,
                    "justify" => TextAlign.Justify,
                    _ => style.TextAlign
                };
                break;

            case CssPropertyNames.LineHeight:
                if (value.Type == CssValueType.Number)
                    style.LineHeight = (float)value.NumericValue;
                else if (value.Type == CssValueType.Length)
                    style.LineHeight = (float)value.NumericValue / style.FontSize;
                else if (value.Type == CssValueType.Percentage)
                    style.LineHeight = (float)value.NumericValue / 100f;
                break;

            case CssPropertyNames.FontWeight:
                style.FontWeight = ResolveFontWeight(value, parentStyle);
                break;

            case CssPropertyNames.FontStyle:
                style.FontStyle = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => FontStyleType.Normal,
                    "italic" => FontStyleType.Italic,
                    "oblique" => FontStyleType.Oblique,
                    _ => style.FontStyle
                };
                break;

            case CssPropertyNames.TextDecoration or CssPropertyNames.TextDecorationLine:
                style.TextDecorationLine = ResolveTextDecorationLine(value);
                break;

            case CssPropertyNames.WhiteSpace:
                style.WhiteSpace = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => WhiteSpaceType.Normal,
                    "pre" => WhiteSpaceType.Pre,
                    "nowrap" => WhiteSpaceType.Nowrap,
                    "pre-wrap" => WhiteSpaceType.PreWrap,
                    "pre-line" => WhiteSpaceType.PreLine,
                    _ => style.WhiteSpace
                };
                break;

            case CssPropertyNames.TextTransform:
                style.TextTransform = value.Raw.ToLowerInvariant() switch
                {
                    "none" => TextTransformType.None,
                    "uppercase" => TextTransformType.Uppercase,
                    "lowercase" => TextTransformType.Lowercase,
                    "capitalize" => TextTransformType.Capitalize,
                    _ => style.TextTransform
                };
                break;

            case CssPropertyNames.LetterSpacing:
                if (value.Raw.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    style.LetterSpacing = 0;
                else
                    style.LetterSpacing = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.WordSpacing:
                if (value.Raw.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    style.WordSpacing = 0;
                else
                    style.WordSpacing = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.WordBreak:
                style.WordBreak = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => WordBreakType.Normal,
                    "break-all" => WordBreakType.BreakAll,
                    "keep-all" => WordBreakType.KeepAll,
                    _ => style.WordBreak
                };
                break;

            case CssPropertyNames.OverflowWrap or CssPropertyNames.WordWrap:
                style.OverflowWrap = value.Raw.ToLowerInvariant() switch
                {
                    "normal" => OverflowWrapType.Normal,
                    "break-word" => OverflowWrapType.BreakWord,
                    "anywhere" => OverflowWrapType.Anywhere,
                    _ => style.OverflowWrap
                };
                break;

            case CssPropertyNames.ListStyleType:
                style.ListStyleType = value.Raw.ToLowerInvariant();
                break;

            case CssPropertyNames.ListStyle:
                // Simplified: treat entire value as list-style-type
                style.ListStyleType = value.Raw.ToLowerInvariant();
                break;

            default:
                return false;
        }
        return true;
    }
}
