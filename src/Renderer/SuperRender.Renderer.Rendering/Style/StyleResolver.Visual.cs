using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyVisualProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.PointerEvents:
                style.PointerEvents = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "auto" or "none" or "visiblepainted" or "visiblefill" or "visiblestroke" or "visible"
                        or "painted" or "fill" or "stroke" or "all" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.PointerEvents,
                };
                break;

            case CssPropertyNames.UserSelect:
                style.UserSelect = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "auto" or "none" or "text" or "all" or "contain" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.UserSelect,
                };
                break;

            case CssPropertyNames.ObjectFit:
                style.ObjectFit = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "fill" or "contain" or "cover" or "none" or "scale-down" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.ObjectFit,
                };
                break;

            case CssPropertyNames.ObjectPosition:
                style.ObjectPosition = value.Raw.Trim().ToLowerInvariant();
                break;

            case CssPropertyNames.WillChange:
                style.WillChange = value.Raw.Trim().ToLowerInvariant();
                break;

            case CssPropertyNames.ColorScheme:
                style.ColorScheme = value.Raw.Trim().ToLowerInvariant();
                break;

            case CssPropertyNames.Appearance:
                style.Appearance = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "auto" or "none" or "menulist-button" or "textfield" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.Appearance,
                };
                break;

            case CssPropertyNames.OutlineWidth:
                style.OutlineWidth = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.OutlineStyle:
                style.OutlineStyle = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "none" or "auto" or "dotted" or "dashed" or "solid" or "double"
                        or "groove" or "ridge" or "inset" or "outset" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.OutlineStyle,
                };
                break;

            case CssPropertyNames.OutlineColor:
                style.OutlineColor = ResolveColor(value, style);
                break;

            case CssPropertyNames.OutlineOffset:
                style.OutlineOffset = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.Outline:
                ParseOutlineShorthand(style, value.Raw, parentStyle);
                break;

            case CssPropertyNames.Resize:
                // Store as-is for now
                break;

            case CssPropertyNames.VerticalAlign:
                ParseVerticalAlign(style, value, parentStyle);
                break;

            case CssPropertyNames.ListStylePosition:
                style.ListStylePosition = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "inside" or "outside" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.ListStylePosition,
                };
                break;

            case CssPropertyNames.CounterReset:
                style.CounterReset = value.Raw.Trim();
                break;

            case CssPropertyNames.CounterIncrement:
                style.CounterIncrement = value.Raw.Trim();
                break;

            case CssPropertyNames.TextDecorationStyle:
                style.TextDecorationStyle = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "solid" or "double" or "dotted" or "dashed" or "wavy" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.TextDecorationStyle,
                };
                break;

            case CssPropertyNames.TextDecorationThickness:
                if (value.Raw.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase)
                    || value.Raw.Trim().Equals("from-font", StringComparison.OrdinalIgnoreCase))
                    style.TextDecorationThickness = float.NaN;
                else
                    style.TextDecorationThickness = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.TextUnderlineOffset:
                if (value.Raw.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase))
                    style.TextUnderlineOffset = float.NaN;
                else
                    style.TextUnderlineOffset = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.TextShadow:
                style.TextShadow = value.Raw.Trim().Equals("none", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : value.Raw.Trim();
                break;

            case CssPropertyNames.FontStretch:
                style.FontStretch = value.Raw.Trim().ToLowerInvariant();
                break;

            case CssPropertyNames.Font:
                ParseFontShorthand(style, value.Raw, parentStyle);
                break;

            // Writing modes
            case CssPropertyNames.WritingMode:
                style.WritingMode = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "horizontal-tb" or "vertical-rl" or "vertical-lr"
                        or "sideways-rl" or "sideways-lr" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.WritingMode,
                };
                break;

            case CssPropertyNames.TextOrientation:
                style.TextOrientation = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "mixed" or "upright" or "sideways" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.TextOrientation,
                };
                break;

            // Scroll snap
            case CssPropertyNames.ScrollSnapType:
                style.ScrollSnapType = value.Raw.Trim().ToLowerInvariant();
                break;

            case CssPropertyNames.ScrollSnapAlign:
                style.ScrollSnapAlign = value.Raw.Trim().ToLowerInvariant();
                break;

            // Container queries
            case CssPropertyNames.ContainerType:
                style.ContainerType = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "normal" or "size" or "inline-size" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.ContainerType,
                };
                break;

            case CssPropertyNames.ContainerName:
                style.ContainerName = value.Raw.Trim();
                break;

            default:
                return false;
        }
        return true;
    }

    private static void ParseVerticalAlign(ComputedStyle style, CssValue value, ComputedStyle? parentStyle)
    {
        var raw = value.Raw.Trim().ToLowerInvariant();
        switch (raw)
        {
            case "baseline":
            case "sub":
            case "super":
            case "top":
            case "text-top":
            case "middle":
            case "bottom":
            case "text-bottom":
                style.VerticalAlign = raw;
                style.VerticalAlignLength = 0;
                break;
            default:
                // Try as a length/percentage
                style.VerticalAlign = "length";
                style.VerticalAlignLength = value.Type == CssValueType.Percentage
                    ? (float)value.NumericValue / 100f * (style.LineHeight * style.FontSize)
                    : value.Type is CssValueType.Length or CssValueType.Number
                        ? (float)value.NumericValue
                        : 0;
                break;
        }
    }

    private void ParseOutlineShorthand(ComputedStyle style, string raw, ComputedStyle? parentStyle)
    {
        var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "none" or "dotted" or "dashed" or "solid" or "double"
                or "groove" or "ridge" or "inset" or "outset" or "auto")
            {
                style.OutlineStyle = lower;
            }
            else if (Document.Color.TryFromName(lower, out var namedColor))
            {
                style.OutlineColor = namedColor;
            }
            else if (lower.StartsWith('#'))
            {
                style.OutlineColor = Document.Color.FromHex(lower);
            }
            else
            {
                var val = CssParser.ParseValueText(part);
                float len = ResolveLength(val, parentStyle);
                if (!float.IsNaN(len))
                    style.OutlineWidth = len;
            }
        }
    }

    private static void ParseFontShorthand(ComputedStyle style, string raw, ComputedStyle? parentStyle)
    {
        // CSS font shorthand: [style] [variant] [weight] [stretch] size[/line-height] family
        var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        int i = 0;

        // Optional: font-style
        if (i < parts.Length && parts[i].ToLowerInvariant() is "italic" or "oblique" or "normal")
        {
            style.FontStyle = parts[i].ToLowerInvariant() switch
            {
                "italic" => FontStyleType.Italic,
                "oblique" => FontStyleType.Oblique,
                _ => FontStyleType.Normal,
            };
            i++;
        }

        // Optional: font-variant
        if (i < parts.Length && parts[i].Equals("small-caps", StringComparison.OrdinalIgnoreCase))
        {
            style.FontVariant = "small-caps";
            i++;
        }

        // Optional: font-weight
        if (i < parts.Length)
        {
            var lower = parts[i].ToLowerInvariant();
            if (lower is "bold" or "bolder" or "lighter" || (int.TryParse(lower, out int w) && w is >= 100 and <= 900))
            {
                var val = CssParser.ParseValueText(parts[i]);
                style.FontWeight = ResolveFontWeight(val, parentStyle);
                i++;
            }
        }

        // Required: font-size (possibly with /line-height)
        if (i < parts.Length)
        {
            var sizeStr = parts[i];
            i++;
            var slashIdx = sizeStr.IndexOf('/', StringComparison.Ordinal);
            if (slashIdx >= 0)
            {
                var sizeVal = CssParser.ParseValueText(sizeStr[..slashIdx]);
                style.FontSize = ResolveFontSize(sizeVal, parentStyle);
                var lhVal = CssParser.ParseValueText(sizeStr[(slashIdx + 1)..]);
                if (lhVal.Type == CssValueType.Number)
                    style.LineHeight = (float)lhVal.NumericValue;
            }
            else
            {
                var sizeVal = CssParser.ParseValueText(sizeStr);
                style.FontSize = ResolveFontSize(sizeVal, parentStyle);
            }
        }

        // Remaining: font-family
        if (i < parts.Length)
        {
            var familyStr = string.Join(" ", parts[i..]);
            style.FontFamilies = Document.Style.FontFamilyParser.Parse(familyStr);
        }
    }
}
