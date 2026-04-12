using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyFlexProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.FlexDirection:
                style.FlexDirection = value.Raw.ToLowerInvariant() switch
                {
                    "row" => FlexDirectionType.Row,
                    "row-reverse" => FlexDirectionType.RowReverse,
                    "column" => FlexDirectionType.Column,
                    "column-reverse" => FlexDirectionType.ColumnReverse,
                    _ => style.FlexDirection
                };
                break;

            case CssPropertyNames.FlexWrap:
                style.FlexWrap = value.Raw.ToLowerInvariant() switch
                {
                    "nowrap" => FlexWrapType.Nowrap,
                    "wrap" => FlexWrapType.Wrap,
                    "wrap-reverse" => FlexWrapType.WrapReverse,
                    _ => style.FlexWrap
                };
                break;

            case CssPropertyNames.FlexFlow:
                ApplyFlexFlowShorthand(style, value.Raw);
                break;

            case CssPropertyNames.JustifyContent:
                style.JustifyContent = value.Raw.ToLowerInvariant() switch
                {
                    "flex-start" => JustifyContentType.FlexStart,
                    "flex-end" => JustifyContentType.FlexEnd,
                    "center" => JustifyContentType.Center,
                    "space-between" => JustifyContentType.SpaceBetween,
                    "space-around" => JustifyContentType.SpaceAround,
                    "space-evenly" => JustifyContentType.SpaceEvenly,
                    _ => style.JustifyContent
                };
                break;

            case CssPropertyNames.AlignItems:
                style.AlignItems = value.Raw.ToLowerInvariant() switch
                {
                    "stretch" => AlignItemsType.Stretch,
                    "flex-start" => AlignItemsType.FlexStart,
                    "flex-end" => AlignItemsType.FlexEnd,
                    "center" => AlignItemsType.Center,
                    "baseline" => AlignItemsType.Baseline,
                    _ => style.AlignItems
                };
                break;

            case CssPropertyNames.AlignSelf:
                style.AlignSelf = value.Raw.ToLowerInvariant() switch
                {
                    "auto" => AlignSelfType.Auto,
                    "stretch" => AlignSelfType.Stretch,
                    "flex-start" => AlignSelfType.FlexStart,
                    "flex-end" => AlignSelfType.FlexEnd,
                    "center" => AlignSelfType.Center,
                    "baseline" => AlignSelfType.Baseline,
                    _ => style.AlignSelf
                };
                break;

            case CssPropertyNames.AlignContent:
                style.AlignContent = value.Raw.ToLowerInvariant() switch
                {
                    "stretch" => AlignContentType.Stretch,
                    "flex-start" => AlignContentType.FlexStart,
                    "flex-end" => AlignContentType.FlexEnd,
                    "center" => AlignContentType.Center,
                    "space-between" => AlignContentType.SpaceBetween,
                    "space-around" => AlignContentType.SpaceAround,
                    "space-evenly" => AlignContentType.SpaceEvenly,
                    _ => style.AlignContent
                };
                break;

            case CssPropertyNames.FlexGrow:
                if (value.Type is CssValueType.Number)
                    style.FlexGrow = (float)value.NumericValue;
                break;

            case CssPropertyNames.FlexShrink:
                if (value.Type is CssValueType.Number)
                    style.FlexShrink = (float)value.NumericValue;
                break;

            case CssPropertyNames.FlexBasis:
                style.FlexBasis = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.Flex:
                ApplyFlexShorthand(style, value);
                break;

            case CssPropertyNames.Gap:
                {
                    var gapVal = ResolveLength(value, parentStyle);
                    if (!float.IsNaN(gapVal))
                    {
                        style.Gap = gapVal;
                        style.RowGap = float.NaN;
                        style.ColumnGap = float.NaN;
                    }
                }
                break;

            case CssPropertyNames.RowGap:
                {
                    var rgVal = ResolveLength(value, parentStyle);
                    if (!float.IsNaN(rgVal))
                        style.RowGap = rgVal;
                }
                break;

            case CssPropertyNames.ColumnGap:
                {
                    var cgVal = ResolveLength(value, parentStyle);
                    if (!float.IsNaN(cgVal))
                        style.ColumnGap = cgVal;
                }
                break;

            default:
                return false;
        }
        return true;
    }

    private static void ApplyFlexShorthand(ComputedStyle style, CssValue value)
    {
        var raw = value.Raw.Trim().ToLowerInvariant();

        switch (raw)
        {
            case "initial":
                style.FlexGrow = 0;
                style.FlexShrink = 1;
                style.FlexBasis = float.NaN; // auto
                return;
            case "auto":
                style.FlexGrow = 1;
                style.FlexShrink = 1;
                style.FlexBasis = float.NaN; // auto
                return;
            case "none":
                style.FlexGrow = 0;
                style.FlexShrink = 0;
                style.FlexBasis = float.NaN; // auto
                return;
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 1 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var grow))
        {
            style.FlexGrow = grow;
            style.FlexShrink = 1;
            style.FlexBasis = 0; // single number: basis = 0

            if (parts.Length >= 2 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var shrink))
            {
                style.FlexShrink = shrink;

                if (parts.Length >= 3)
                {
                    if (parts[2] == "auto")
                        style.FlexBasis = float.NaN;
                    else
                        style.FlexBasis = ParseLengthValue(parts[2]);
                }
                else
                {
                    style.FlexBasis = 0;
                }
            }
        }
    }

    private static float ParseLengthValue(string raw)
    {
        if (raw == "auto") return float.NaN;
        if (raw == "0") return 0;

        // Try parsing number with unit
        int numEnd = 0;
        while (numEnd < raw.Length && (char.IsDigit(raw[numEnd]) || raw[numEnd] == '.' || raw[numEnd] == '-'))
            numEnd++;

        if (numEnd > 0 && float.TryParse(raw[..numEnd], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            var unit = raw[numEnd..].Trim().ToLowerInvariant();
            return unit switch
            {
                "px" or "" => num,
                _ => num
            };
        }

        return float.NaN;
    }

    private static void ApplyFlexFlowShorthand(ComputedStyle style, string raw)
    {
        var parts = raw.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            switch (part)
            {
                case "row":
                    style.FlexDirection = FlexDirectionType.Row;
                    break;
                case "row-reverse":
                    style.FlexDirection = FlexDirectionType.RowReverse;
                    break;
                case "column":
                    style.FlexDirection = FlexDirectionType.Column;
                    break;
                case "column-reverse":
                    style.FlexDirection = FlexDirectionType.ColumnReverse;
                    break;
                case "nowrap":
                    style.FlexWrap = FlexWrapType.Nowrap;
                    break;
                case "wrap":
                    style.FlexWrap = FlexWrapType.Wrap;
                    break;
                case "wrap-reverse":
                    style.FlexWrap = FlexWrapType.WrapReverse;
                    break;
            }
        }
    }
}
