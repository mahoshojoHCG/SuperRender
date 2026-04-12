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
                    _ => style.Display
                };
                break;

            case CssPropertyNames.Position:
                style.Position = value.Raw.ToLowerInvariant() switch
                {
                    "static" => PositionType.Static,
                    "relative" => PositionType.Relative,
                    "absolute" => PositionType.Absolute,
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

            case CssPropertyNames.Overflow or CssPropertyNames.OverflowX or CssPropertyNames.OverflowY:
                style.Overflow = value.Raw.ToLowerInvariant() switch
                {
                    "visible" => OverflowType.Visible,
                    "hidden" => OverflowType.Hidden,
                    "scroll" => OverflowType.Scroll,
                    "auto" => OverflowType.Auto,
                    _ => style.Overflow
                };
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
}
