using SuperRender.Document.Css;
using SuperRender.Renderer.Rendering.Layout;

namespace SuperRender.Renderer.Rendering.Style;

public sealed partial class StyleResolver
{
    private bool ApplyPositionProperty(ComputedStyle style, string prop, CssValue value, ComputedStyle? parentStyle)
    {
        switch (prop)
        {
            case CssPropertyNames.Display:
                style.Display = value.Raw.ToLowerInvariant() switch
                {
                    "block" => DisplayType.Block,
                    "inline" => DisplayType.Inline,
                    "inline-block" => DisplayType.InlineBlock,
                    "flow-root" => DisplayType.FlowRoot,
                    "none" => DisplayType.None,
                    "flex" => DisplayType.Flex,
                    "inline-flex" => DisplayType.InlineFlex,
                    "contents" => DisplayType.Contents,
                    "list-item" => DisplayType.ListItem,
                    "grid" => DisplayType.Grid,
                    "inline-grid" => DisplayType.InlineGrid,
                    "table" => DisplayType.Table,
                    "inline-table" => DisplayType.InlineTable,
                    "table-row" => DisplayType.TableRow,
                    "table-cell" => DisplayType.TableCell,
                    "table-row-group" => DisplayType.TableRowGroup,
                    "table-header-group" => DisplayType.TableHeaderGroup,
                    "table-footer-group" => DisplayType.TableFooterGroup,
                    "table-column" => DisplayType.TableColumn,
                    "table-column-group" => DisplayType.TableColumnGroup,
                    "table-caption" => DisplayType.TableCaption,
                    _ => style.Display
                };
                break;

            case CssPropertyNames.Position:
                style.Position = value.Raw.ToLowerInvariant() switch
                {
                    "static" => PositionType.Static,
                    "relative" => PositionType.Relative,
                    "absolute" => PositionType.Absolute,
                    "fixed" => PositionType.Fixed,
                    "sticky" => PositionType.Sticky,
                    _ => style.Position
                };
                break;

            case CssPropertyNames.Top:
                style.Top = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.Left:
                style.Left = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.Right:
                style.Right = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.Bottom:
                style.Bottom = ResolveLength(value, parentStyle);
                break;

            case CssPropertyNames.ZIndex:
                if (value.Raw.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    style.ZIndex = 0;
                    style.ZIndexIsAuto = true;
                }
                else if (value.Type == CssValueType.Number)
                {
                    style.ZIndex = (int)value.NumericValue;
                    style.ZIndexIsAuto = false;
                }
                break;

            case CssPropertyNames.Overflow:
            {
                var ov = ParseOverflowValue(value.Raw, style.Overflow);
                style.Overflow = ov;
                style.OverflowX = ov;
                style.OverflowY = ov;
                break;
            }
            case CssPropertyNames.OverflowX:
            {
                style.OverflowX = ParseOverflowValue(value.Raw, style.OverflowX);
                break;
            }
            case CssPropertyNames.OverflowY:
            {
                style.OverflowY = ParseOverflowValue(value.Raw, style.OverflowY);
                break;
            }

            // Inset logical properties (mapped to physical in LTR horizontal-tb)
            case CssPropertyNames.InsetBlockStart:
                style.Top = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.InsetBlockEnd:
                style.Bottom = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.InsetInlineStart:
                style.Left = ResolveLength(value, parentStyle);
                break;
            case CssPropertyNames.InsetInlineEnd:
                style.Right = ResolveLength(value, parentStyle);
                break;

            // Aspect ratio
            case CssPropertyNames.AspectRatio:
                style.AspectRatio = ParseAspectRatio(value.Raw);
                break;

            // P1: Inherited text properties
            case CssPropertyNames.Visibility:
                style.Visibility = value.Raw.ToLowerInvariant() switch
                {
                    "visible" => VisibilityType.Visible,
                    "hidden" => VisibilityType.Hidden,
                    "collapse" => VisibilityType.Collapse,
                    _ => style.Visibility
                };
                break;

            case CssPropertyNames.TextOverflow:
                style.TextOverflow = value.Raw.ToLowerInvariant() switch
                {
                    "clip" => TextOverflowType.Clip,
                    "ellipsis" => TextOverflowType.Ellipsis,
                    _ => style.TextOverflow
                };
                break;

            case CssPropertyNames.Cursor:
                style.Cursor = value.Raw.ToLowerInvariant() switch
                {
                    "auto" => CursorType.Auto,
                    "default" => CursorType.Default,
                    "pointer" => CursorType.Pointer,
                    "text" => CursorType.Text,
                    "crosshair" => CursorType.Crosshair,
                    "move" => CursorType.Move,
                    "not-allowed" => CursorType.NotAllowed,
                    "wait" => CursorType.Wait,
                    "help" => CursorType.Help,
                    _ => style.Cursor
                };
                break;

            default:
                return false;
        }
        return true;
    }

    private static OverflowType ParseOverflowValue(string raw, OverflowType fallback)
    {
        return raw.ToLowerInvariant() switch
        {
            "visible" => OverflowType.Visible,
            "hidden" => OverflowType.Hidden,
            "scroll" => OverflowType.Scroll,
            "auto" => OverflowType.Auto,
            "clip" => OverflowType.Clip,
            _ => fallback
        };
    }

    private static float ParseAspectRatio(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return float.NaN;
        }

        // Try "W / H" format
        var slashIndex = trimmed.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex >= 0)
        {
            var wStr = trimmed[..slashIndex].Trim();
            var hStr = trimmed[(slashIndex + 1)..].Trim();
            if (float.TryParse(wStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float w)
                && float.TryParse(hStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float h)
                && h > 0)
            {
                return w / h;
            }
        }

        // Try single number
        if (float.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float single)
            && single > 0)
        {
            return single;
        }

        return float.NaN;
    }
}
