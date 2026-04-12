using SuperRender.Document.Css;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyGridProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.GridTemplateRows:
                style.GridTemplateRows = value.Raw.Trim();
                break;
            case CssPropertyNames.GridTemplateColumns:
                style.GridTemplateColumns = value.Raw.Trim();
                break;
            case CssPropertyNames.GridTemplateAreas:
                style.GridTemplateAreas = value.Raw.Trim();
                break;
            case CssPropertyNames.GridRowStart:
                style.GridRowStart = value.Raw.Trim();
                break;
            case CssPropertyNames.GridRowEnd:
                style.GridRowEnd = value.Raw.Trim();
                break;
            case CssPropertyNames.GridColumnStart:
                style.GridColumnStart = value.Raw.Trim();
                break;
            case CssPropertyNames.GridColumnEnd:
                style.GridColumnEnd = value.Raw.Trim();
                break;
            case CssPropertyNames.GridRow:
                ParseGridLineShorthand(value.Raw, out string? rs, out string? re);
                style.GridRowStart = rs;
                style.GridRowEnd = re;
                break;
            case CssPropertyNames.GridColumn:
                ParseGridLineShorthand(value.Raw, out string? cs, out string? ce);
                style.GridColumnStart = cs;
                style.GridColumnEnd = ce;
                break;
            case CssPropertyNames.GridArea:
                ParseGridAreaShorthand(style, value.Raw);
                break;
            case CssPropertyNames.GridAutoRows:
                style.GridAutoRows = value.Raw.Trim();
                break;
            case CssPropertyNames.GridAutoColumns:
                style.GridAutoColumns = value.Raw.Trim();
                break;
            case CssPropertyNames.GridAutoFlow:
                style.GridAutoFlow = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "row" or "column" or "row dense" or "column dense" or "dense" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.GridAutoFlow,
                };
                break;

            // Float and clear
            case CssPropertyNames.Float:
                style.Float = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "left" or "right" or "none" or "inline-start" or "inline-end" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.Float,
                };
                break;
            case CssPropertyNames.Clear:
                style.Clear = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "left" or "right" or "both" or "none" or "inline-start" or "inline-end" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.Clear,
                };
                break;

            // Table
            case CssPropertyNames.TableLayout:
                style.TableLayout = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "auto" or "fixed" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.TableLayout,
                };
                break;
            case CssPropertyNames.BorderCollapse:
                style.BorderCollapse = value.Raw.Trim().ToLowerInvariant() switch
                {
                    "separate" or "collapse" => value.Raw.Trim().ToLowerInvariant(),
                    _ => style.BorderCollapse,
                };
                break;
            case CssPropertyNames.BorderSpacing:
                style.BorderSpacing = ResolveLength(value, parentStyle);
                break;

            default:
                return false;
        }
        return true;
    }

    private static void ParseGridLineShorthand(string raw, out string? start, out string? end)
    {
        var parts = raw.Split('/', StringSplitOptions.TrimEntries);
        start = parts.Length >= 1 ? parts[0] : null;
        end = parts.Length >= 2 ? parts[1] : null;
    }

    private static void ParseGridAreaShorthand(ComputedStyle style, string raw)
    {
        var parts = raw.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length >= 1) style.GridRowStart = parts[0];
        if (parts.Length >= 2) style.GridColumnStart = parts[1];
        if (parts.Length >= 3) style.GridRowEnd = parts[2];
        if (parts.Length >= 4) style.GridColumnEnd = parts[3];
    }
}
